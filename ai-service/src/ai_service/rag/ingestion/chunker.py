"""
Structure-aware chunking (AI-RAG-001): split on headings first, then paragraphs, into 400–800 tokens with 10–15 %
overlap. Table rows and numbered clauses (articles) are never split in the middle. Each chunk carries its heading path.
Token counts are approximated (Arabic ≈ 1 token per 3.2 characters, Latin ≈ 1 per 4).
"""

import re
from dataclasses import dataclass, field

HEADING = re.compile(r"^(#{1,6})\s+(.+?)\s*$")
TABLE_ROW = re.compile(r"^\s*\|.*\|\s*$")
CLAUSE = re.compile(
    r"^\s*((?:المادة|الماده|البند|الفصل|الباب|القسم)\s*[\(\[]?\s*[\d٠-٩]+[\)\]]?|"
    r"(?:article|section|clause|chapter)\s+\d+[a-z]?|[\d٠-٩]+(?:[.\-][\d٠-٩]+)*[.)\-]\s)",
    re.IGNORECASE,
)
PAGE_BREAK = re.compile(r"^\f?\[\[page (\d+)\]\]\s*$")

MIN_TOKENS = 400
MAX_TOKENS = 800
OVERLAP_RATIO = 0.12


@dataclass(frozen=True, slots=True)
class Block:
    """One paragraph, table row or heading line with the page it starts on."""

    text: str
    page: int
    kind: str  # "heading" | "paragraph" | "table" | "clause"
    level: int = 0


@dataclass(slots=True)
class Chunk:
    text: str
    heading_path: str
    page: int
    tokens: int
    ordinal: int = 0
    blocks: list[Block] = field(default_factory=list)


def estimate_tokens(text: str) -> int:
    arabic = sum(1 for c in text if "؀" <= c <= "ۿ")
    other = len(text) - arabic
    return int(arabic / 3.2 + other / 4) + 1


def parse_blocks(text: str) -> list[Block]:
    """Markdown-ish text → blocks. Page markers `[[page N]]` come from the extractor; tables keep every row whole."""
    blocks: list[Block] = []
    page = 1
    paragraph: list[str] = []

    def flush() -> None:
        if paragraph:
            body = " ".join(line.strip() for line in paragraph).strip()
            if body:
                kind = "clause" if CLAUSE.match(body) else "paragraph"
                blocks.append(Block(body, page, kind))
            paragraph.clear()

    for raw in text.splitlines():
        line = raw.rstrip()
        if (m := PAGE_BREAK.match(line)) is not None:
            flush()
            page = int(m.group(1))
            continue
        if not line.strip():
            flush()
            continue
        if (h := HEADING.match(line)) is not None:
            flush()
            blocks.append(Block(h.group(2), page, "heading", len(h.group(1))))
            continue
        if TABLE_ROW.match(line):
            flush()
            if set(line.replace("|", "").strip()) <= set("-: "):
                continue  # markdown table separator
            blocks.append(Block(line.strip(), page, "table"))
            continue
        if CLAUSE.match(line) and paragraph:
            flush()  # a numbered clause starts its own block
        paragraph.append(line)
    flush()
    return blocks


def chunk_text(text: str) -> list[Chunk]:
    """Greedy packing of blocks under the token budget, with a tail overlap carried into the next chunk."""
    chunks: list[Chunk] = []
    path: dict[int, str] = {}
    current: list[Block] = []
    current_tokens = 0

    def heading_path() -> str:
        return " › ".join(path[level] for level in sorted(path))

    def emit() -> None:
        nonlocal current, current_tokens
        if not current:
            return
        body = "\n".join(b.text for b in current)
        chunks.append(Chunk(body, heading_path(), current[0].page, estimate_tokens(body), len(chunks), list(current)))
        # Overlap: keep the last blocks worth ~12 % of the budget (never a heading).
        budget = int(MAX_TOKENS * OVERLAP_RATIO)
        tail: list[Block] = []
        kept = 0
        for block in reversed(current):
            t = estimate_tokens(block.text)
            if kept + t > budget or block.kind == "heading":
                break
            tail.insert(0, block)
            kept += t
        current = tail
        current_tokens = kept

    for block in parse_blocks(text):
        if block.kind == "heading":
            if current_tokens >= MIN_TOKENS:
                emit()
                current, current_tokens = [], 0  # a new section never starts with overlap from the previous one
            path = {level: title for level, title in path.items() if level < block.level}
            path[block.level] = block.text
            continue
        tokens = estimate_tokens(block.text)
        if current and current_tokens + tokens > MAX_TOKENS:
            emit()
        if tokens > MAX_TOKENS and block.kind not in {"table", "clause"}:
            for piece in _split_long(block.text):
                current.append(Block(piece, block.page, block.kind))
                current_tokens += estimate_tokens(piece)
                if current_tokens >= MAX_TOKENS:
                    emit()
            continue
        current.append(block)
        current_tokens += tokens
    emit()
    return [c for c in chunks if c.text.strip()]


def _split_long(text: str) -> list[str]:
    """Oversized paragraph without structure: split on sentence ends, then by words."""
    sentences = re.split(r"(?<=[.!?؟。])\s+", text)
    pieces: list[str] = []
    buf = ""
    for sentence in sentences:
        if estimate_tokens(buf + " " + sentence) > MAX_TOKENS and buf:
            pieces.append(buf.strip())
            buf = sentence
        else:
            buf = (buf + " " + sentence).strip()
    if buf:
        pieces.append(buf)
    return pieces
