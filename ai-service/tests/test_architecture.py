"""Invariant 8 (ADR-0006): Azure libraries are imported only inside adapter modules."""

import ast
from pathlib import Path

SRC = Path(__file__).resolve().parents[1] / "src" / "ai_service"


def test_azure_is_imported_only_in_adapters() -> None:
    offenders = []
    for path in SRC.rglob("*.py"):
        if "adapters" in path.parts:
            continue
        for node in ast.walk(ast.parse(path.read_text(encoding="utf-8"))):
            names = (
                [a.name for a in node.names]
                if isinstance(node, ast.Import)
                else [node.module or ""]
                if isinstance(node, ast.ImportFrom)
                else []
            )
            offenders += [f"{path.relative_to(SRC)}: {n}" for n in names if n.split(".")[0] == "azure"]

    assert offenders == []
