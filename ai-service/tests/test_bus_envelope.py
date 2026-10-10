from uuid import uuid4

from ai_service.messaging.bus import envelope, exchange_name
from ai_service.sessions.contracts import TurnCompleted, TurnFailed


def test_envelope_matches_masstransit_contract() -> None:
    message = TurnCompleted(
        session_id=uuid4(), turn_id=uuid4(), text="نص", citations=[], input_tokens=1, output_tokens=2, cached_tokens=0
    )

    env = envelope(message)

    assert exchange_name(message) == "Majlis.Framework.Domain.Events:TurnCompleted"
    assert env["messageType"] == ["urn:message:Majlis.Framework.Domain.Events:TurnCompleted"]
    assert env["message"] == {
        "sessionId": str(message.session_id),
        "turnId": str(message.turn_id),
        "text": "نص",
        "citations": [],
        "inputTokens": 1,
        "outputTokens": 2,
        "cachedTokens": 0,
    }


def test_failed_message_uses_its_own_exchange() -> None:
    message = TurnFailed(session_id=uuid4(), turn_id=uuid4(), reason_key="General:Errors:AiBusy")

    assert exchange_name(message) == "Majlis.Framework.Domain.Events:TurnFailed"
    assert envelope(message)["message"]["reasonKey"] == "General:Errors:AiBusy"  # type: ignore[index]
