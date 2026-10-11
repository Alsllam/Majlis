"""
Minutes draft from a transcript (FR-MTG-003): one model call with the versioned prompt, JSON out, validated
before it leaves. People edit and approve the draft in the Meetings module; nothing here is final.
"""

import json
import re
from datetime import date

from pydantic import BaseModel, Field, ValidationError

from ai_service.llm.models import ModelRole
from ai_service.llm.prompts import load_prompt
from ai_service.llm.provider import ChatMessage, LlmInvalidOutputError, LlmProvider, TextDelta
from ai_service.messaging.contracts import ActionItemContract, MinutesGenerated, MinutesRequested
from ai_service.settings import Settings

MAX_TRANSCRIPT_CHARS = 120_000
_FENCE = re.compile(r"^\s*```(?:json)?\s*|\s*```\s*$", re.IGNORECASE)


class _ActionItem(BaseModel):
    text: str = Field(min_length=1, max_length=500)
    owner_name: str | None = Field(default=None, alias="ownerName", max_length=120)
    due_date: date | None = Field(default=None, alias="dueDate")

    model_config = {"populate_by_name": True, "extra": "ignore"}


class MinutesDraft(BaseModel):
    """What the model must answer (the shape in `prompts/minutes.v1.md`)."""

    summary: str = Field(min_length=1, max_length=8000)
    decisions: list[str] = Field(default_factory=list)
    open_questions: list[str] = Field(default_factory=list, alias="openQuestions")
    action_items: list[_ActionItem] = Field(default_factory=list, alias="actionItems")

    model_config = {"populate_by_name": True, "extra": "ignore"}


def parse_minutes(text: str) -> MinutesDraft:
    """Tolerates code fences and prose around the object; anything else is an invalid output."""
    cleaned = _FENCE.sub("", text.strip())
    start, end = cleaned.find("{"), cleaned.rfind("}")
    if start < 0 or end <= start:
        raise LlmInvalidOutputError
    try:
        return MinutesDraft.model_validate(json.loads(cleaned[start : end + 1]))
    except (ValueError, ValidationError) as exc:
        raise LlmInvalidOutputError from exc


class MinutesGenerator:
    def __init__(self, settings: Settings, llm: LlmProvider) -> None:
        self._settings = settings
        self._llm = llm
        self._prompt = load_prompt(settings.minutes_prompt_version)

    async def generate(self, request: MinutesRequested) -> MinutesGenerated:
        instructions = f"{self._prompt}\n\nLanguage of the minutes: `{request.language}`."
        transcript = request.transcript[:MAX_TRANSCRIPT_CHARS]
        attendees = "، ".join(request.attendees) if request.attendees else "-"
        user = (
            f"Title: {request.title}\nDate: {request.held_at.date().isoformat()}\nAttendees: {attendees}\n\n"
            f"<transcript>\n{transcript}\n</transcript>"
        )
        parts: list[str] = []
        async for event in self._llm.stream(
            ModelRole.CHAT,
            instructions,
            [ChatMessage("user", user)],
            temperature=0.1,
            max_output_tokens=self._settings.minutes_max_output_tokens,
        ):
            if isinstance(event, TextDelta):
                parts.append(event.text)
        draft = parse_minutes("".join(parts))
        return MinutesGenerated(
            tenant_id=request.tenant_id,
            meeting_id=request.meeting_id,
            summary=draft.summary.strip(),
            decisions=_clean(draft.decisions),
            open_questions=_clean(draft.open_questions),
            action_items=[
                ActionItemContract(text=i.text.strip(), owner_name=i.owner_name, due_date=i.due_date)
                for i in draft.action_items
                if i.text.strip()
            ],
            language=request.language,
        )


def _clean(values: list[str]) -> list[str]:
    return [v.strip() for v in values if v and v.strip()]
