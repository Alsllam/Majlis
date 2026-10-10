"""Validates access tokens issued by Majlis.Auth.Host (OpenIddict, RS256 JWT) against its JWKS."""

import asyncio
from dataclasses import dataclass, field
from uuid import UUID

import jwt
from jwt import PyJWKClient


@dataclass(frozen=True, slots=True)
class Principal:
    """The caller. Tenant and user always come from here, never from a request body (invariant 3)."""

    user_id: UUID
    tenant_id: UUID
    display_name: str
    roles: tuple[str, ...] = field(default_factory=tuple)
    raw_token: str = field(default="", repr=False)


class InvalidTokenError(Exception):
    pass


class TokenValidator:
    def __init__(self, issuer: str, audience: str, jwks_url: str) -> None:
        self._issuer = issuer
        self._audience = audience
        self._jwks = PyJWKClient(jwks_url, cache_keys=True, lifespan=3600)

    async def validate(self, token: str) -> Principal:
        try:
            key = await asyncio.to_thread(self._jwks.get_signing_key_from_jwt, token)
            claims = jwt.decode(
                token,
                key.key,
                algorithms=["RS256", "RS384", "RS512", "PS256"],
                audience=self._audience,
                issuer=self._issuer,
                options={"require": ["exp", "iss", "aud", "sub"]},
                leeway=30,
            )
        except (jwt.PyJWTError, jwt.PyJWKClientError) as exc:
            raise InvalidTokenError(str(exc)) from exc

        try:
            roles = claims.get("role", [])
            return Principal(
                user_id=UUID(claims["sub"]),
                tenant_id=UUID(claims["tenant_id"]),
                display_name=str(claims.get("name", "")),
                roles=tuple([roles] if isinstance(roles, str) else roles),
                raw_token=token,
            )
        except (KeyError, ValueError) as exc:
            raise InvalidTokenError("Token is missing user or tenant claims.") from exc
