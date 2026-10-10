import asyncio
import json
from uuid import uuid4

import fakeredis

from ai_service.llm.provider import LlmContentBlockedError
from ai_service.rag.models import IndexedChunk, SearchHit
from ai_service.rag.retrieval.search import Retriever
from ai_service.sessions.contracts import AiHistoryTurn, TurnCompleted, TurnFailed, TurnStopped
from ai_service.sessions.stream import cancel_key, heartbeat_key, stream_channel, text_key
from ai_service.sessions.turn_runner import TurnRunner, build_messages
from ai_service.settings import Settings
from tests.conftest import FakeLlm, FakePublisher, turn_request


async def run_capturing(runner: TurnRunner, redis: fakeredis.FakeAsyncRedis, session_id, request, on_first_delta=None):  # type: ignore[no-untyped-def]
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
    result = await runner.run(session_id, request)
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
    result = await TurnRunner(settings, llm, redis, publisher, retriever).run(uuid4(), request)

    assert isinstance(result, TurnCompleted)
    assert [c.label for c in result.citations] == ["S1"]
    assert result.citations[0].document_id == doc
    assert result.citations[0].page == 3
    assert retriever.calls == [(request.instruction, [f"ws:{request.workspace_id}", f"room:{request.room_id}"])]
    assert '<source id="S1"' in llm.instructions[-1]
