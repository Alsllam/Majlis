from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse

router = APIRouter(prefix="/health", tags=["health"])


@router.get("/live")
async def live() -> dict[str, str]:
    return {"status": "ok"}


@router.get("/ready")
async def ready(request: Request) -> JSONResponse:
    state = request.app.state
    checks = {
        "redis": await _redis_ok(request),
        "rabbitmq": bool(state.publisher.is_connected),
        "llm_configured": bool(state.settings.llm_configured),
    }
    return JSONResponse(
        {"status": "ok" if all(checks.values()) else "degraded", "checks": checks}, 200 if all(checks.values()) else 503
    )


async def _redis_ok(request: Request) -> bool:
    try:
        return bool(await request.app.state.redis.ping())
    except Exception:
        return False
