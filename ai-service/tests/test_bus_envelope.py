import json
from uuid import uuid4

from ai_service.messaging.bus import alias_of, body_of, exchange_name
from ai_service.sessions.contracts import TurnCompleted, TurnFailed


def test_completed_message_is_plain_camel_case_json_with_alias() -> None:
    message = TurnCompleted(
        session_id=uuid4(), turn_id=uuid4(), text="نص", citations=[], input_tokens=1, output_tokens=2, cached_tokens=0
    )

    assert alias_of(message) == "turn-completed"
    assert exchange_name(message) == "majlis.turn-completed"
    assert json.loads(body_of(message)) == {
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

    assert exchange_name(message) == "majlis.turn-failed"
    assert json.loads(body_of(message))["reasonKey"] == "General:Errors:AiBusy"
