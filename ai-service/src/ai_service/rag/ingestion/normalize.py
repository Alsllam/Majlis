"""
Arabic normalization for the *search copy* of a text (AI-RAG-002). The original text is kept for display and
citation; only `content_search` and query strings go through `normalize_for_search`.
"""

import re
import unicodedata

_TATWEEL = "ـ"
_DIACRITICS = re.compile(r"[ؐ-ًؚ-ٰٟۖ-ۭ]")
_ALEF_FORMS = str.maketrans({"أ": "ا", "إ": "ا", "آ": "ا", "ٱ": "ا"})
_ARABIC_INDIC = str.maketrans("٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹", "01234567890123456789")
_SPACE_BEFORE_PUNCT = re.compile(r"\s+([،؛؟,;:!?.])")
_MULTI_SPACE = re.compile(r"[ \t]+")
_ARABIC_LETTERS = re.compile(r"[؀-ۿ]")
_LATIN_LETTERS = re.compile(r"[A-Za-z]")


def normalize_for_search(text: str) -> str:
    """Unify alef forms, ya/alef maqsura and taa marbuta, drop tatweel and diacritics, normalize digits and spacing."""
    text = unicodedata.normalize("NFKC", text)
    text = text.replace(_TATWEEL, "")
    text = _DIACRITICS.sub("", text)
    text = text.translate(_ALEF_FORMS)
    text = text.replace("ى", "ي").replace("ة", "ه")
    text = text.translate(_ARABIC_INDIC)
    text = _SPACE_BEFORE_PUNCT.sub(r"\1", text)
    text = _MULTI_SPACE.sub(" ", text)
    return text.strip().lower()


def clean_display(text: str) -> str:
    """Light cleanup that keeps the original wording: NFC, digits as written, fixed spacing."""
    text = unicodedata.normalize("NFC", text)
    text = _SPACE_BEFORE_PUNCT.sub(r"\1", text)
    text = _MULTI_SPACE.sub(" ", text)
    return text.strip()


def detect_language(text: str) -> str:
    """`ar` when Arabic letters dominate, else `en` (good enough for routing analyzers and the answer language)."""
    arabic = len(_ARABIC_LETTERS.findall(text))
    latin = len(_LATIN_LETTERS.findall(text))
    return "ar" if arabic >= latin else "en"
