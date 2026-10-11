"""`draft_document`: arguments as the Knowledge module expects them (`DraftDocumentToolArgs`)."""

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class DraftDocumentArgs(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")

    title: str = Field(min_length=1, max_length=200, description="Document title, in the participant's language.")
    content_markdown: str = Field(
        min_length=1,
        max_length=60000,
        description=(
            "The full draft in Markdown (headings, paragraphs, lists, tables). Write the complete text, not a summary."
        ),
    )
    type: Literal["Other", "Regulation", "Contract", "Policy", "Sop", "Report"] = Field(default="Other")
    language: Literal["ar", "en"] | None = Field(default=None, description="Language of the draft.")


def summarize_draft_document(args: DraftDocumentArgs, language: str) -> str:
    """One line shown on the approval card, in the session's language."""
    words = len(args.content_markdown.split())
    return (
        f"صياغة مستند: {args.title} ({words} كلمة تقريباً)"
        if language == "ar"
        else f"Draft document: {args.title} (about {words} words)"
    )
