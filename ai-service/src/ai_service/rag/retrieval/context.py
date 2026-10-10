"""Context assembly (skill §6 step 5) and citation resolution (AI-RAG-005)."""

import re
from collections.abc import Sequence
from dataclasses import dataclass

from ai_service.rag.ingestion.chunker import estimate_tokens
from ai_service.rag.models import SearchHit
from ai_service.sessions.contracts import Citation

CITATION = re.compile(r"\[S(\d+)\]")


@dataclass(frozen=True, slots=True)
class SourceContext:
    text: str
    sources: dict[str, SearchHit]

    @property
    def labels(self) -> list[str]:
        return list(self.sources)


def _escape(value: str) -> str:
    return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def build_context(hits: Sequence[SearchHit], budget_tokens: int) -> SourceContext:
    """Wraps the best chunks as `<source id="S#" …>` until the token budget is spent; keeps the label → hit map."""
    parts: list[str] = []
    sources: dict[str, SearchHit] = {}
    used = 0
    for hit in hits:
        label = f"S{len(sources) + 1}"
        block = (
            f'<source id="{label}" title="{_escape(hit.chunk.title)}" page="{hit.chunk.page}"'
            f' path="{_escape(hit.chunk.heading_path)}">\n{hit.chunk.content}\n</source>'
        )
        tokens = estimate_tokens(block)
        if used + tokens > budget_tokens and sources:
            break
        parts.append(block)
        sources[label] = hit
        used += tokens
    return SourceContext("\n\n".join(parts), sources)


def resolve_citations(text: str, context: SourceContext) -> list[Citation]:
    """The `[S#]` markers the model used, in first-use order, resolved to document, version, page and passage."""
    citations: list[Citation] = []
    seen: set[str] = set()
    for number in CITATION.findall(text):
        label = f"S{number}"
        hit = context.sources.get(label)
        if hit is None or label in seen:
            continue
        seen.add(label)
        passage = hit.chunk.content
        citations.append(
            Citation(
                label=label,
                document_id=hit.chunk.document_id,
                version_id=hit.chunk.version_id,
                title=hit.chunk.title,
                page=hit.chunk.page or None,
                passage=passage[:300] + ("…" if len(passage) > 300 else ""),
            )
        )
    return citations
