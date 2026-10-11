"""Request dependencies: the authenticated caller."""

from typing import Annotated

from fastapi import Depends, Request

from ai_service.api.errors import AiServiceError
from ai_service.security.tokens import InvalidTokenError, Principal, TokenValidator


def get_token_validator(request: Request) -> TokenValidator:
    validator: TokenValidator = request.app.state.token_validator
    return validator


async def get_principal(
    request: Request, validator: Annotated[TokenValidator, Depends(get_token_validator)]
) -> Principal:
    header = request.headers.get("authorization", "")
    scheme, _, token = header.partition(" ")
    if scheme.lower() != "bearer" or not token:
        raise AiServiceError(401, "General:Errors:Unauthorized")
    try:
        return await validator.validate(token)
    except InvalidTokenError as exc:
        raise AiServiceError(401, "General:Errors:Unauthorized") from exc


CurrentPrincipal = Annotated[Principal, Depends(get_principal)]
