"""Model roles. Deployment names are configuration (skill §4)."""

from enum import StrEnum


class ModelRole(StrEnum):
    CHAT = "chat"
    FAST = "fast"
    REASONING = "reasoning"
    EMBED = "embed"
    STT = "stt"
    TTS = "tts"
