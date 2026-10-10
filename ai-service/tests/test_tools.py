"""Tool registry: schemas sent to the model, argument validation, approval summaries."""

import pytest

from ai_service.llm.tools import TOOLS, ToolValidationError, get_tool, tool_definitions
from ai_service.llm.tools.create_task import CreateTaskArgs


def test_create_task_definition_matches_the_responses_function_shape() -> None:
    (definition,) = tool_definitions()

    assert definition["type"] == "function"
    assert definition["name"] == "create_task"
    assert definition["parameters"]["required"] == ["title"]
    assert definition["parameters"]["additionalProperties"] is False
    assert set(definition["parameters"]["properties"]) == {
        "title",
        "description",
        "assigneeName",
        "dueDate",
        "priority",
    }
    assert TOOLS["create_task"].mutates
    assert TOOLS["create_task"].risk == "Low"


def test_parse_accepts_camel_case_and_rejects_unknown_fields() -> None:
    tool = get_tool("create_task")
    assert tool is not None

    args = tool.parse('{"title": "مراجعة العقد", "assigneeName": "سارة", "dueDate": "2026-11-01", "priority": "High"}')
    assert isinstance(args, CreateTaskArgs)
    assert args.model_dump(mode="json", by_alias=True, exclude_none=True) == {
        "title": "مراجعة العقد",
        "assigneeName": "سارة",
        "dueDate": "2026-11-01",
        "priority": "High",
    }

    with pytest.raises(ToolValidationError, match="title"):
        tool.parse('{"description": "بدون عنوان"}')
    with pytest.raises(ToolValidationError, match="Extra"):
        tool.parse('{"title": "x", "owner": "y"}')
    with pytest.raises(ToolValidationError):
        tool.parse("not json")


@pytest.mark.parametrize(
    ("language", "expected"),
    [
        ("ar", "إنشاء مهمة: مراجعة العقد · مسندة إلى سارة · تستحق في 2026-11-01"),
        ("en", "Create task: مراجعة العقد · assigned to سارة · due 2026-11-01"),
    ],
)
def test_summary_is_in_the_session_language(language: str, expected: str) -> None:
    tool = get_tool("create_task")
    assert tool is not None
    args = tool.parse('{"title": "مراجعة العقد", "assigneeName": "سارة", "dueDate": "2026-11-01"}')

    assert tool.summarize(args, language) == expected
