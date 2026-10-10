"""
Tool registry. Each tool declares its JSON schema (sent to the model), whether it mutates data, its risk and the
permission the driver needs. Mutating tools produce approval requests in the Approvals module (invariant 1); the
owning .NET module runs them after a person approves. Names and risk levels match `KnownTools` in Majlis.Approvals.
"""

from collections.abc import Callable
from dataclasses import dataclass
from typing import Any, Literal

from pydantic import BaseModel, ValidationError

from ai_service.llm.tools.create_task import CreateTaskArgs, summarize_create_task

Risk = Literal["Low", "Medium", "High"]


class ToolValidationError(Exception):
    """The model sent arguments that do not match the schema; the text goes back to the model as the tool output."""


@dataclass(frozen=True, slots=True)
class ToolSpec:
    name: str
    description: str
    args_model: type[BaseModel]
    mutates: bool
    risk: Risk
    required_permission: str
    summarize: Callable[[Any, str], str]

    @property
    def parameters(self) -> dict[str, Any]:
        schema = self.args_model.model_json_schema()
        schema.pop("title", None)
        schema["additionalProperties"] = False
        return schema

    def parse(self, arguments: str) -> BaseModel:
        try:
            return self.args_model.model_validate_json(arguments or "{}")
        except ValidationError as exc:
            details = "; ".join(f"{'.'.join(str(p) for p in e['loc'])}: {e['msg']}" for e in exc.errors())
            raise ToolValidationError(details) from exc

    def definition(self) -> dict[str, Any]:
        """Responses API function tool definition."""
        return {
            "type": "function",
            "name": self.name,
            "description": self.description,
            "parameters": self.parameters,
            "strict": False,
        }


TOOLS: dict[str, ToolSpec] = {
    "create_task": ToolSpec(
        name="create_task",
        description=(
            "Propose a new task for the team (needs a person's approval before it is created). "
            "Use it when a participant asks to create, add or assign a task or an action item."
        ),
        args_model=CreateTaskArgs,
        mutates=True,
        risk="Low",
        required_permission="Permissions.Approvals.RequestAction",
        summarize=summarize_create_task,
    ),
}


def get_tool(name: str) -> ToolSpec | None:
    return TOOLS.get(name)


def tool_definitions() -> list[dict[str, Any]]:
    return [tool.definition() for tool in TOOLS.values()]
