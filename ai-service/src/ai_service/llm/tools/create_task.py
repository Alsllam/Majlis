"""`create_task`: arguments as the Tasks module expects them (`CreateTaskToolArgs`)."""

from datetime import date
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class CreateTaskArgs(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")

    title: str = Field(min_length=1, max_length=200, description="Short task title, in the participant's language.")
    description: str | None = Field(default=None, max_length=2000, description="What has to be done and why.")
    assignee_name: str | None = Field(
        default=None, max_length=100, description="Display name of the participant to assign it to, if named."
    )
    due_date: date | None = Field(default=None, description="Due date (YYYY-MM-DD), only when stated.")
    priority: Literal["Low", "Normal", "High", "Urgent"] = Field(default="Normal")


def summarize_create_task(args: CreateTaskArgs, language: str) -> str:
    """One line shown on the approval card, in the session's language."""
    if language == "ar":
        parts = [f"إنشاء مهمة: {args.title}"]
        if args.assignee_name:
            parts.append(f"مسندة إلى {args.assignee_name}")
        if args.due_date:
            parts.append(f"تستحق في {args.due_date.isoformat()}")
    else:
        parts = [f"Create task: {args.title}"]
        if args.assignee_name:
            parts.append(f"assigned to {args.assignee_name}")
        if args.due_date:
            parts.append(f"due {args.due_date.isoformat()}")
    return " · ".join(parts)
