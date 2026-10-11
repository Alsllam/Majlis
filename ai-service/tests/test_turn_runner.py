import asyncio
import json
from uuid import UUID, uuid4

import fakeredis

from ai_service.llm.provider import FunctionCall, LlmContentBlockedError, ToolCallMessage, ToolResultMessage
from ai_service.llm.tools import tool_definitions
from ai_service.rag.models import IndexedChunk, SearchHit
from ai_service.rag.retrieval.search import Retriever
from ai_service.sessions.approvals import ApprovalRequest, ApprovalRequestError
from ai_service.sessions.contracts import AiHistoryTurn, TurnCompleted, TurnFailed, TurnStopped
from ai_service.sessions.stream import cancel_key, heartbeat_key, stream_channel, text_key
from ai_service.sessions.turn_runner import TurnRunner, build_messages
from ai_service.settings import Settings
from tests.conftest import FakeLlm, FakePublisher, turn_request


async def run_capturing(
    runner: TurnRunner, redis: fakeredis.FakeAsyncRedis, session_id, request, *, on_first_delta=None, bearer_token=""
):  # type: ignore[no-untyped-def]
    pubsub = redis.pubsub()
    await pubsub.subscribe(stream_channel(session_id))
    messages: list[dict[str, object]] = []

    async def reader() -> None:
        async for m in pubsub.listen():
            if m["type"] == "message":
                messages.append(json.loads(m["data"]))
                if on_first_delta and len(messages) == 1:
                    await on_first_delta()

    task = asyncio.create_task(reader())
    result = await runner.run(session_id, request, bearer_token)
    await asyncio.sleep(0.05)
    task.cancel()
    await pubsub.aclose()
    return result, messages


async def test_run_streams_coalesced_deltas_and_publishes_completed(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    llm, publisher = FakeLlm(["وفقاً ", "للمادة ", "١٢، ", "مدة الإشعار ", "٦٠ يوماً."]), FakePublisher()
    session_id, request = uuid4(), turn_request()

    result, messages = await run_capturing(TurnRunner(settings, llm, redis, publisher), redis, session_id, request)

    assert isinstance(result, TurnCompleted)
    assert result.text == "وفقاً للمادة ١٢، مدة الإشعار ٦٠ يوماً."
    assert (result.input_tokens, result.output_tokens, result.cached_tokens) == (120, 30, 64)
    assert publisher.published == [result]
    assert "".join(m["data"]["text"] for m in messages) == result.text  # type: ignore[index]
    assert [m["chunk"] for m in messages] == list(range(1, len(messages) + 1))
    assert all(m["type"] == "turn.delta" and m["turnId"] == str(request.turn_id) for m in messages)
    snapshot = await redis.hgetall(text_key(request.turn_id))
    assert snapshot["text"] == result.text
    assert int(snapshot["chunk"]) == len(messages)
    assert not await redis.exists(heartbeat_key(request.turn_id))


async def test_run_stops_with_partial_text_when_cancel_key_is_set(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    llm, publisher = FakeLlm(["أ"] * 50, delay_s=0.02), FakePublisher()
    session_id, request = uuid4(), turn_request()

    async def press_stop() -> None:
        await redis.set(cancel_key(request.turn_id), "1")

    result, _ = await run_capturing(
        TurnRunner(settings, llm, redis, publisher), redis, session_id, request, on_first_delta=press_stop
    )

    assert isinstance(result, TurnStopped)
    assert 0 < len(result.partial_text) < 50
    assert publisher.published == [result]
    assert not await redis.exists(cancel_key(request.turn_id))


async def test_run_fails_with_content_blocked_and_keeps_partial(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    llm, publisher = FakeLlm(["جزء ", "أول"], error=LlmContentBlockedError()), FakePublisher()

    result = await TurnRunner(settings, llm, redis, publisher).run(uuid4(), turn_request())

    assert isinstance(result, TurnFailed)
    assert result.reason_key == "General:Errors:ContentBlocked"
    assert result.partial_text == "جزء أول"


async def test_run_fails_with_busy_when_turn_times_out(settings: Settings, redis: fakeredis.FakeAsyncRedis) -> None:
    slow = FakeLlm(["."] * 100, delay_s=0.05)
    publisher = FakePublisher()

    result = await TurnRunner(settings.model_copy(update={"turn_timeout_s": 0.3}), slow, redis, publisher).run(
        uuid4(), turn_request()
    )

    assert isinstance(result, TurnFailed)
    assert result.reason_key == "General:Errors:AiBusy"


async def test_heartbeat_is_refreshed_while_turn_runs(settings: Settings, redis: fakeredis.FakeAsyncRedis) -> None:
    llm, request = FakeLlm(["x"] * 10, delay_s=0.03), turn_request()
    seen: list[bool] = []

    async def watch() -> None:
        for _ in range(5):
            await asyncio.sleep(0.05)
            seen.append(bool(await redis.exists(heartbeat_key(request.turn_id))))

    await asyncio.gather(TurnRunner(settings, llm, redis, FakePublisher()).run(uuid4(), request), watch())

    assert any(seen)


def test_build_messages_prefixes_speaker_and_keeps_history_order() -> None:
    request = turn_request(
        history=[AiHistoryTurn(instruction="ما مدة العقد؟", answer="سنتان.", instructed_by="خالد")],
        instruction="وما شروط التجديد؟",
    )

    messages = build_messages(request)

    assert [(m.role, m.content) for m in messages] == [
        ("user", "خالد: ما مدة العقد؟"),
        ("assistant", "سنتان."),
        ("user", "سارة: وما شروط التجديد؟"),
    ]


async def test_run_grounds_the_answer_and_resolves_citations(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    """With a retriever, the instructions carry the sources and `[S#]` markers become citations."""
    doc = uuid4()
    hit = SearchHit(
        IndexedChunk(
            id="c1",
            tenant_id=uuid4(),
            document_id=doc,
            version_id=uuid4(),
            title="عقد المورد",
            heading_path="",
            page=3,
            language="ar",
            doc_type="Contract",
            acl_groups=["ws:x"],
            content="غرامة التأخير واحد بالمائة.",
            content_search="غرامه التاخير",
            content_vector=[],
            ordinal=0,
            content_sha256="s",
        ),
        0.9,
    )

    class FakeRetriever(Retriever):
        def __init__(self) -> None:
            self.calls: list[tuple[str, list[str]]] = []

        async def retrieve(self, *, tenant_id, acl_groups, question):  # type: ignore[no-untyped-def]
            self.calls.append((question, list(acl_groups)))
            return [hit]

    retriever = FakeRetriever()
    llm = FakeLlm(["الغرامة واحد بالمائة ", "[S1]."])
    publisher = FakePublisher()
    request = turn_request()
    result = await TurnRunner(settings, llm, redis, publisher, retriever=retriever).run(uuid4(), request)

    assert isinstance(result, TurnCompleted)
    assert [c.label for c in result.citations] == ["S1"]
    assert result.citations[0].document_id == doc
    assert result.citations[0].page == 3
    assert retriever.calls == [(request.instruction, [f"ws:{request.workspace_id}", f"room:{request.room_id}"])]
    assert '<source id="S1"' in llm.instructions[-1]


class FakeApprovals:
    """Records approval requests; `error` makes every call fail."""

    def __init__(self, error: Exception | None = None) -> None:
        self.error = error
        self.requests: list[tuple[ApprovalRequest, str, str]] = []

    async def create(self, request: ApprovalRequest, bearer_token: str, language: str) -> UUID:
        self.requests.append((request, bearer_token, language))
        if self.error is not None:
            raise self.error
        return UUID(int=len(self.requests))


CALL = FunctionCall("call_1", "create_task", '{"title": "مراجعة العقد", "assigneeName": "سارة", "priority": "High"}')


async def test_tool_call_becomes_an_approval_request_and_the_model_gets_the_outcome(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    llm = FakeLlm([], rounds=[["سأقترح ", "مهمة.", CALL], ["أُرسل طلب إنشاء المهمة للموافقة."]])
    approvals, publisher = FakeApprovals(), FakePublisher()
    session_id, request = uuid4(), turn_request(instruction="أنشئ مهمة لمراجعة العقد وأسندها إلى سارة")
    runner = TurnRunner(settings, llm, redis, publisher, approvals=approvals)  # type: ignore[arg-type]

    result, messages = await run_capturing(runner, redis, session_id, request, bearer_token="tok-driver")

    assert isinstance(result, TurnCompleted)
    assert result.text == "سأقترح مهمة.أُرسل طلب إنشاء المهمة للموافقة."
    assert (result.input_tokens, result.output_tokens) == (240, 60)
    # The approval request carries the session, the validated args and the driver's token.
    ((approval, token, language),) = approvals.requests
    assert (approval.session_id, approval.turn_id, approval.tool, token, language) == (
        session_id,
        request.turn_id,
        "create_task",
        "tok-driver",
        "ar",
    )
    assert approval.args == {"title": "مراجعة العقد", "assigneeName": "سارة", "priority": "High"}
    assert approval.summary == "إنشاء مهمة: مراجعة العقد · مسندة إلى سارة"
    # Round two feeds the call and its output back to the model, with the tools still offered.
    assert len(llm.calls) == 2
    assert llm.tools == [tool_definitions(), tool_definitions()]
    assert llm.calls[1][-2:] == [
        ToolCallMessage("call_1", "create_task", CALL.arguments),
        ToolResultMessage(
            "call_1",
            json.dumps(
                {"status": "pending_approval", "requestId": str(UUID(int=1)), "summary": approval.summary},
                ensure_ascii=False,
            ),
        ),
    ]
    progress = next(m for m in messages if m["type"] == "turn.progress")
    assert progress["data"]["key"] == "Session.Progress.RequestingApproval"  # type: ignore[index]
    assert "## Tools and approvals" in llm.instructions[0]


async def test_invalid_tool_arguments_and_approval_errors_go_back_to_the_model(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    bad = FunctionCall("call_1", "create_task", '{"description": "بدون عنوان"}')
    unknown = FunctionCall("call_2", "delete_everything", "{}")
    llm = FakeLlm([], rounds=[[bad], [unknown], [CALL], ["اعتذر، تعذّر إرسال الطلب."]])
    approvals = FakeApprovals(error=ApprovalRequestError("Approvals:Request:NotWorkspaceMember", 403))
    runner = TurnRunner(settings, llm, redis, FakePublisher(), approvals=approvals)  # type: ignore[arg-type]

    result = await runner.run(uuid4(), turn_request())

    assert isinstance(result, TurnCompleted)
    assert result.text == "اعتذر، تعذّر إرسال الطلب."
    outputs = [json.loads(i.output) for i in llm.calls[-1] if isinstance(i, ToolResultMessage)]
    assert outputs[0]["status"] == "error"
    assert "title" in outputs[0]["message"]
    assert outputs[1] == {"status": "error", "message": "Unknown tool 'delete_everything'."}
    assert outputs[2] == {"status": "rejected", "message": "Approvals:Request:NotWorkspaceMember"}
    assert len(approvals.requests) == 1


async def test_tool_loop_stops_offering_tools_on_the_last_round(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    settings = settings.model_copy(update={"max_tool_rounds": 2})
    llm = FakeLlm(["نص ", "أخير"], rounds=[[CALL]])
    runner = TurnRunner(settings, llm, redis, FakePublisher(), approvals=FakeApprovals())  # type: ignore[arg-type]

    result = await runner.run(uuid4(), turn_request())

    assert isinstance(result, TurnCompleted)
    assert result.text == "نص أخير"
    assert llm.tools == [[*tool_definitions()], None]


async def test_without_an_approvals_client_no_tools_are_offered(
    settings: Settings, redis: fakeredis.FakeAsyncRedis
) -> None:
    llm = FakeLlm(["مرحبا"])

    await TurnRunner(settings, llm, redis, FakePublisher()).run(uuid4(), turn_request())

    assert llm.tools == [None]
