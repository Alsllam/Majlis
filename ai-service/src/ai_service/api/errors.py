"""Errors in the backend shape `{ "error": { code, date, messages[], source } }`, so client handlers work unchanged."""

from datetime import UTC, datetime

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

MESSAGES: dict[str, dict[str, str]] = {
    "General:Errors:Unauthorized": {"ar": "يجب تسجيل الدخول.", "en": "You need to sign in."},
    "General:Business:Forbidden": {
        "ar": "ليست لديك صلاحية لتنفيذ هذا الإجراء.",
        "en": "You do not have permission to do this.",
    },
    "General:Errors:InvalidRequest": {"ar": "صيغة الطلب غير صحيحة.", "en": "The request is not valid."},
    "General:Errors:AiBusy": {
        "ar": "خدمة الذكاء الاصطناعي مشغولة حالياً. حاول مرة أخرى بعد قليل.",
        "en": "The AI service is busy right now. Please try again shortly.",
    },
    "General:Errors:AiNotConfigured": {
        "ar": "لم يتم إعداد نموذج الذكاء الاصطناعي في هذه البيئة.",
        "en": "No AI model is configured in this environment.",
    },
    "General:Errors:ContentBlocked": {
        "ar": "تعذّر إكمال الطلب لأن المحتوى يخالف سياسة الاستخدام.",
        "en": "The request was blocked by the content policy.",
    },
    "General:Errors:Unexpected": {
        "ar": "حدث خطأ غير متوقع. حاول مرة أخرى لاحقاً.",
        "en": "Something went wrong. Please try again later.",
    },
}


def localize(key: str, accept_language: str | None) -> str:
    lang = "en" if (accept_language or "").lower().startswith("en") else "ar"
    return MESSAGES.get(key, {}).get(lang, key)


class AiServiceError(Exception):
    """An error that is safe to show: an HTTP status and a localization key."""

    def __init__(self, status_code: int, key: str) -> None:
        super().__init__(key)
        self.status_code = status_code
        self.key = key


def error_body(status_code: int, messages: list[str]) -> dict[str, object]:
    return {
        "error": {
            "code": str(status_code),
            "date": datetime.now(UTC).isoformat(),
            "messages": messages,
            "source": "Ai",
        }
    }


def register_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(AiServiceError)
    async def _service_error(request: Request, exc: AiServiceError) -> JSONResponse:
        message = localize(exc.key, request.headers.get("accept-language"))
        return JSONResponse(error_body(exc.status_code, [message]), status_code=exc.status_code)

    @app.exception_handler(RequestValidationError)
    async def _validation_error(request: Request, exc: RequestValidationError) -> JSONResponse:
        message = localize("General:Errors:InvalidRequest", request.headers.get("accept-language"))
        return JSONResponse(error_body(400, [message]), status_code=400)
