"""Approvals client: the driver's token is forwarded; backend errors map to a key the model is told about."""

from uuid import uuid4

import httpx
import pytest

from ai_service.sessions.approvals import ApprovalRequest, ApprovalRequestError, ApprovalsClient


def request() -> ApprovalRequest:
    return ApprovalRequest(uuid4(), uuid4(), uuid4(), uuid4(), "create_task", {"title": "x"}, "إنشاء مهمة: x", None)


def client_with(handler) -> ApprovalsClient:  # type: ignore[no-untyped-def]
    return ApprovalsClient(
        "http://bff/api/approvals",
        client=httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://bff/api/approvals"),
    )


async def test_create_posts_with_the_drivers_token_and_returns_the_id() -> None:
    seen: list[httpx.Request] = []
    request_id = uuid4()

    def handler(req: httpx.Request) -> httpx.Response:
        seen.append(req)
        return httpx.Response(200, json=str(request_id))

    result = await client_with(handler).create(request(), "tok-123", "ar")

    assert result == request_id
    assert seen[0].url == "http://bff/api/approvals/requests"
    assert seen[0].headers["authorization"] == "Bearer tok-123"
    assert seen[0].headers["accept-language"] == "ar"
    body = seen[0].read()
    assert b'"tool":"create_task"' in body
    assert b'"summary"' in body


@pytest.mark.parametrize(
    ("status", "payload", "expected"),
    [
        (
            403,
            {"error": {"messages": ["Approvals:Request:NotWorkspaceMember"]}},
            "Approvals:Request:NotWorkspaceMember",
        ),
        (400, {"error": {"messages": []}}, "http 400"),
        (502, "gateway", "http 502"),
    ],
)
async def test_create_raises_with_the_backend_key(status: int, payload: object, expected: str) -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        return (
            httpx.Response(status, json=payload)
            if not isinstance(payload, str)
            else httpx.Response(status, text=payload)
        )

    with pytest.raises(ApprovalRequestError) as exc:
        await client_with(handler).create(request(), "tok", "en")

    assert exc.value.key == expected
    assert exc.value.status == status


async def test_create_maps_connection_errors_to_service_unavailable() -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("refused")

    with pytest.raises(ApprovalRequestError) as exc:
        await client_with(handler).create(request(), "tok", "en")

    assert exc.value.key == "General:Errors:ServiceUnavailable"
