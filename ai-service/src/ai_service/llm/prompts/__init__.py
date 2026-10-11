"""Versioned prompt files. A prompt change is a new version file, never an edit (skill §15.3)."""

from functools import cache
from importlib import resources


@cache
def load_prompt(name: str) -> str:
    return resources.files(__package__).joinpath(f"{name}.md").read_text(encoding="utf-8")
