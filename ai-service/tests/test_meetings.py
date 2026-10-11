"""Minutes from a transcript and transcription of a recording: parsing, failure keys, the worker's answers."""

from datetime import date, datetime
from uuid import uuid4

import pytest

from ai_service.llm.provider import LlmBusyError, LlmInvalidOutputError, LlmNotConfiguredError
from ai_service.meetings.minutes import MinutesGenerator, parse_minutes
from ai_service.messaging.bus import alias_of, body_of
from ai_service.messaging.contracts import (
    MinutesFailed,
    MinutesGenerated,
    MinutesRequested,
    TranscriptionCompleted,
    TranscriptionFailed,
    TranscriptionRequested,
)
from ai_service.workers.meetings_worker import MeetingsWorker
from tests.conftest import TENANT, FakeLlm, FakePublisher

MINUTES_JSON = (
    '{"summary": "ناقش الفريق العقد الجديد واتفق على مراجعة البنود.", "decisions": ["مراجعة بنود العقد", " "], '
    '"openQuestions": ["هل نحتاج مستشاراً؟"], "actionItems": [{"text": "إرسال المسودة", "ownerName": "خالد", '
    '"dueDate": "2026-10-12"}, {"text": "  ", "ownerName": null, "dueDate": null}]}'
)


def minutes_request(**overrides: object) -> MinutesRequested:
    data: dict[str, object] = {
        "tenantId": TENANT,
        "meetingId": uuid4(),
        "title": "اجتماع المتابعة",
        "heldAt": datetime(2026, 10, 5, 9, 0),
        "attendees": ["سارة", "خالد"],
        "language": "ar",
        "transcript": "ناقشنا العقد. خالد يرسل المسودة الأسبوع القادم.",
    }
    data.update(overrides)
    return MinutesRequested.model_validate(data)


class TranscribingLlm(FakeLlm):
    def __init__(self, text: str = "", error: Exception | None = None) -> None:
        super().__init__([])
        self.text = text
        self.transcribe_error = error
        self.files: list[tuple[str, str, str | None]] = []

    async def transcribe(self, data: bytes, file_name: str, content_type: str, language: str | None) -> str:
        self.files.append((file_name, content_type, language))
        if self.transcribe_error is not None:
            raise self.transcribe_error
        return self.text


class FakeBlobs:
    def __init__(self, files: dict[str, bytes]) -> None:
        self.files = files

    async def download(self, path: str) -> bytes:
        return self.files[path]

    async def aclose(self) -> None:
        return None


def test_parse_minutes_tolerates_fences_and_prose_and_drops_blank_entries() -> None:
    draft = parse_minutes("Here you go:\n```json\n" + MINUTES_JSON + "\n```\n")
    assert draft.summary.startswith("ناقش الفريق")
    assert draft.action_items[0].due_date == date(2026, 10, 12)
    assert draft.action_items[0].owner_name == "خالد"


@pytest.mark.parametrize("text", ["", "no json here", '{"decisions": []}', '{"summary": "x", "actionItems": [{}]}'])
def test_parse_minutes_rejects_other_shapes(text: str) -> None:
    with pytest.raises(LlmInvalidOutputError):
        parse_minutes(text)


async def test_generator_builds_the_prompt_and_the_message(settings) -> None:  # type: ignore[no-untyped-def]
    llm = FakeLlm([MINUTES_JSON[:40], MINUTES_JSON[40:]], delay_s=0)
    request = minutes_request()

    result = await MinutesGenerator(settings, llm).generate(request)

    assert result.meeting_id == request.meeting_id
    assert result.decisions == ["مراجعة بنود العقد"]
    assert result.open_questions == ["هل نحتاج مستشاراً؟"]
    assert [i.text for i in result.action_items] == ["إرسال المسودة"]
    assert result.language == "ar"
    assert "Language of the minutes: `ar`" in llm.instructions[0]
    assert "never invent" in llm.instructions[0]
    user = llm.calls[0][0]
    assert "Date: 2026-10-05" in getattr(user, "content", "")
    assert "<transcript>" in getattr(user, "content", "")
    assert alias_of(result) == "minutes-generated"
    assert b'"actionItems"' in body_of(result)
    assert b'"dueDate": "2026-10-12"' in body_of(result)


async def test_worker_reports_minutes_failures_with_a_key(settings) -> None:  # type: ignore[no-untyped-def]
    busy = FakeLlm([], delay_s=0, error=LlmBusyError())
    worker = MeetingsWorker(None, MinutesGenerator(settings, busy), busy, FakePublisher(), None)
    failed = await worker.handle_minutes(minutes_request())
    assert isinstance(failed, MinutesFailed)
    assert failed.reason_key == "General:Errors:AiBusy"

    garbage = FakeLlm(["not json"], delay_s=0)
    worker = MeetingsWorker(None, MinutesGenerator(settings, garbage), garbage, FakePublisher(), None)
    failed = await worker.handle_minutes(minutes_request())
    assert isinstance(failed, MinutesFailed)
    assert failed.reason_key == "General:Errors:AiInvalidOutput"

    good = FakeLlm([MINUTES_JSON], delay_s=0)
    worker = MeetingsWorker(None, MinutesGenerator(settings, good), good, FakePublisher(), None)
    assert isinstance(await worker.handle_minutes(minutes_request()), MinutesGenerated)


async def test_worker_transcribes_a_recording_and_reports_every_failure(settings) -> None:  # type: ignore[no-untyped-def]
    request = TranscriptionRequested(
        tenant_id=TENANT, meeting_id=uuid4(), blob_path="t/w/m/recording.mp3", content_type="audio/mpeg", language="ar"
    )
    llm = TranscribingLlm(" نص التفريغ ")
    generator = MinutesGenerator(settings, llm)

    # No recordings store (storage not configured).
    unconfigured = MeetingsWorker(None, generator, llm, FakePublisher(), None)
    result = await unconfigured.handle_transcription(request)
    assert isinstance(result, TranscriptionFailed)
    assert result.reason_key == "Meetings:Transcription:NotConfigured"

    blobs = FakeBlobs({"t/w/m/recording.mp3": b"\x00" * 10})
    worker = MeetingsWorker(None, generator, llm, FakePublisher(), blobs, max_recording_mb=1)
    done = await worker.handle_transcription(request)
    assert isinstance(done, TranscriptionCompleted)
    assert (done.text, done.language) == ("نص التفريغ", "ar")
    assert llm.files == [("recording.mp3", "audio/mpeg", "ar")]
    assert alias_of(done) == "transcription-completed"

    # Missing blob, too large, model not configured, empty text.
    missing = await worker.handle_transcription(request.model_copy(update={"blob_path": "nope"}))
    assert isinstance(missing, TranscriptionFailed)
    assert missing.reason_key == "Meetings:Transcription:RecordingUnavailable"
    big_file = FakeBlobs({"t/w/m/recording.mp3": b"\x00" * 2_000_000})
    big = MeetingsWorker(None, generator, llm, FakePublisher(), big_file, max_recording_mb=1)
    too_large = await big.handle_transcription(request)
    assert isinstance(too_large, TranscriptionFailed)
    assert too_large.reason_key == "Meetings:Transcription:TooLarge"
    off = MeetingsWorker(None, generator, TranscribingLlm(error=LlmNotConfiguredError()), FakePublisher(), blobs)
    not_configured = await off.handle_transcription(request)
    assert isinstance(not_configured, TranscriptionFailed)
    assert not_configured.reason_key == "General:Errors:AiNotConfigured"
    empty = MeetingsWorker(None, generator, TranscribingLlm("   "), FakePublisher(), blobs)
    nothing = await empty.handle_transcription(request)
    assert isinstance(nothing, TranscriptionFailed)
    assert nothing.reason_key == "Meetings:Transcription:Empty"
