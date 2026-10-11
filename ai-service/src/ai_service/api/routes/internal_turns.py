"""Internal endpoint called by Rooms; not routed by the BFF (only `/ai-api/**` is public)."""

from uuid import UUID

from fastapi import APIRouter, BackgroundTasks, Request, status

from ai_service.api.deps import CurrentPrincipal
from ai_service.api.errors import AiServiceError
from ai_service.sessions.contracts import StartAiTurnRequest
from ai_service.sessions.turn_runner import TurnRunner

router = APIRouter(prefix="/internal/sessions", tags=["internal"])


@router.post("/{session_id}/turns", status_code=status.HTTP_202_ACCEPTED)
async def start_turn(
    session_id: UUID,
    body: StartAiTurnRequest,
    principal: CurrentPrincipal,
    request: Request,
    background: BackgroundTasks,
) -> dict[str, str]:
    """
    Accepts a turn and runs it in the background with the driver's identity (the forwarded token, also used
    for the approval requests the agent's tools create).
    The tenant in the body must match the token; the token wins (invariant 3).
    """
    if body.tenant_id != principal.tenant_id or body.instructed_by.user_id != principal.user_id:
        raise AiServiceError(403, "General:Business:Forbidden")
    if not request.app.state.settings.llm_configured:
        raise AiServiceError(503, "General:Errors:AiNotConfigured")

    runner: TurnRunner = request.app.state.turn_runner
    background.add_task(runner.run, session_id, body, principal.raw_token)
    return {"turnId": str(body.turn_id)}
