"""Chunker and normalizer (skill §14: Arabic text, numbered articles, tables)."""

from ai_service.rag.ingestion.chunker import MAX_TOKENS, MIN_TOKENS, chunk_text, estimate_tokens, parse_blocks
from ai_service.rag.ingestion.normalize import detect_language, normalize_for_search

ARTICLE = (
    "المادة {n}: يلتزم المورد بتسليم البضاعة خلال ثلاثين يوماً من تاريخ أمر الشراء، "
    "ويتحمل غرامة تأخير قدرها واحد بالمائة عن كل أسبوع. "
)


def test_normalize_for_search_unifies_arabic_forms_and_digits() -> None:
    normalized = normalize_for_search("الإدارةُ العامّة ـــ ٢٠٢٦ إلى")
    assert normalized == "الاداره العامه 2026 الي"
    assert normalize_for_search("Article 12 ") == "article 12"


def test_detect_language() -> None:
    assert detect_language("المادة الأولى من اللائحة") == "ar"
    assert detect_language("Article 1 of the regulation") == "en"


def test_parse_blocks_keeps_headings_tables_pages_and_clauses() -> None:
    text = (
        "[[page 1]]\n# الباب الأول\n\n| البند | القيمة |\n|---|---|\n| أ | ١ |\n\n"
        "المادة 1: نص المادة.\nالمادة 2: نص آخر.\n[[page 2]]\nفقرة عادية"
    )
    blocks = parse_blocks(text)
    kinds = [(b.kind, b.page) for b in blocks]
    assert kinds == [("heading", 1), ("table", 1), ("table", 1), ("clause", 1), ("clause", 1), ("paragraph", 2)]


def test_chunk_text_respects_the_budget_and_carries_heading_paths() -> None:
    text = "# الباب الثالث\n\n## الفصل الأول\n\n" + "\n\n".join(ARTICLE.format(n=n) for n in range(1, 40))
    chunks = chunk_text(text)
    assert len(chunks) > 1
    assert all(c.tokens <= MAX_TOKENS + 50 for c in chunks)
    assert all(c.heading_path == "الباب الثالث › الفصل الأول" for c in chunks)
    # Overlap: the first block of chunk 2 appears at the end of chunk 1.
    first_of_second = chunks[1].blocks[0].text
    assert first_of_second in chunks[0].text


def test_chunk_text_never_splits_a_clause_or_table_row() -> None:
    long_clause = "المادة 7: " + "كلمة " * 1200
    text = "| عمود | " + "قيمة " * 900 + "|\n\n" + long_clause
    chunks = chunk_text(text)
    joined = [c.text for c in chunks]
    assert any(long_clause.strip() in t for t in joined)
    assert estimate_tokens(long_clause) > MAX_TOKENS  # oversized on purpose, still whole


def test_chunk_text_starts_a_new_chunk_at_a_heading_once_big_enough() -> None:
    text = "# A\n\n" + "sentence one. " * 300 + "\n\n# B\n\nshort"
    chunks = chunk_text(text)
    assert chunks[0].heading_path == "A"
    assert chunks[-1].heading_path == "B"
    assert chunks[0].tokens >= MIN_TOKENS
