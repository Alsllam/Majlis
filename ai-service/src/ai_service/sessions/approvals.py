"""
Approvals client: a mutating tool call becomes an approval request in the Approvals module (invariant 1),
created with the driver's own token so the requester is the person in control, never a service identity.
"""

import json
from dataclasses import dataclass
from typing import Any
from uuid import UUID

import httpx

from ai_service.observability import get_logger

log = get_logger(__name__)


class ApprovalRequestError(Exception):
    """Approvals refused or could not be reached; the model is told and answers without the action."""

    def __init__(self, key: str, status: int | None = None) -> None:
        super().__init__(key)
        self.key = key
        self.status = status


@dataclass(frozen=True, slots=True)
class ApprovalRequest:
    workspace_id: UUID
    room_id: UUID
    session_id: UUID
    turn_id: UUID
    tool: str
    args: dict[str, Any]
    summary: str
    reason: str | None


class ApprovalsClient:
    def __init__(self, base_url: str, timeout_s: float = 10.0, client: httpx.AsyncClient | None = None) -> None:
        self._client = client or httpx.AsyncClient(base_url=base_url.rstrip("/"), timeout=timeout_s)

    async def create(self, request: ApprovalRequest, bearer_token: str, language: str) -> UUID:
        body = {
            "workspaceId": str(request.workspace_id),
            "roomId": str(request.room_id),
            "sessionId": str(request.session_id),
            "turnId": str(request.turn_id),
            "tool": request.tool,
            "args": request.args,
            "summary": request.summary,
            "reason": request.reason,
        }
        headers = {"authorization": f"Bearer {bearer_token}", "accept-language": language}
        try:
            response = await self._client.post("/requests", json=body, headers=headers)
        except httpx.HTTPError as exc:
            log.warning("approval_request_unreachable", error=type(exc).__name__)
            raise ApprovalRequestError("General:Errors:ServiceUnavailable") from exc
        if response.status_code >= 400:
            key = _error_key(response) or f"http {response.status_code}"
            log.warning("approval_request_rejected", status=response.status_code, key=key)
            raise ApprovalRequestError(key, response.status_code)
        return UUID(str(response.json()))

    async def aclose(self) -> None:
        await self._client.aclose()


def _error_key(response: httpx.Response) -> str | None:
    """The backend error shape: `{"error": {"messages": [...], ...}}`."""
    try:
        error = response.json().get("error") or {}
        messages = error.get("messages") or []
        return str(messages[0]) if messages else None
    except (ValueError, AttributeError, json.JSONDecodeError):
        return None
