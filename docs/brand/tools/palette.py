"""Majlis color system: scales generated in OKLCH, semantic tokens per theme, WCAG checks.

Source of truth for every color in the product. Edit the hue/chroma/lightness
parameters here, then run `build.py` to regenerate tokens and assets.
"""

from __future__ import annotations

import math

# ---------------------------------------------------------------- color math


def _oklch_to_linear_srgb(lightness: float, chroma: float, hue: float) -> tuple[float, float, float]:
    a = chroma * math.cos(math.radians(hue))
    b = chroma * math.sin(math.radians(hue))
    l_ = (lightness + 0.3963377774 * a + 0.2158037573 * b) ** 3
    m_ = (lightness - 0.1055613458 * a - 0.0638541728 * b) ** 3
    s_ = (lightness - 0.0894841775 * a - 1.2914855480 * b) ** 3
    return (
        4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_,
        -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_,
        -0.0041960863 * l_ - 0.7034186147 * m_ + 1.7076147010 * s_,
    )


def _in_gamut(rgb: tuple[float, float, float]) -> bool:
    return all(-1e-4 <= c <= 1 + 1e-4 for c in rgb)


def oklch(lightness: float, chroma: float, hue: float) -> str:
    """OKLCH → sRGB hex, reducing chroma until the color fits in sRGB."""
    while chroma > 0 and not _in_gamut(_oklch_to_linear_srgb(lightness, chroma, hue)):
        chroma -= 0.002
    rgb = _oklch_to_linear_srgb(lightness, max(chroma, 0), hue)

    def encode(c: float) -> int:
        c = min(max(c, 0.0), 1.0)
        c = 12.92 * c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055
        return round(c * 255)

    return "#" + "".join(f"{encode(c):02x}" for c in rgb)


def luminance(hex_color: str) -> float:
    def lin(c: int) -> float:
        s = c / 255
        return s / 12.92 if s <= 0.04045 else ((s + 0.055) / 1.055) ** 2.4

    r, g, b = (int(hex_color[i : i + 2], 16) for i in (1, 3, 5))
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)


def contrast(fg: str, bg: str) -> float:
    hi, lo = sorted((luminance(fg), luminance(bg)), reverse=True)
    return (hi + 0.05) / (lo + 0.05)


# ---------------------------------------------------------------- scales

STEPS = (50, 100, 200, 300, 400, 500, 600, 700, 800, 900)
LIGHTNESS = (0.975, 0.935, 0.870, 0.780, 0.680, 0.585, 0.500, 0.425, 0.355, 0.285)
CHROMA_CURVE = (0.18, 0.35, 0.60, 0.85, 1.0, 1.0, 0.95, 0.85, 0.72, 0.60)  # × peak chroma

# name: (hue, peak chroma). Hues chosen for the majlis: palm-teal, brass (dallah), sand neutrals.
HUES = {
    "brand": (183, 0.105),   # Nakhla teal — primary
    "accent": (72, 0.150),   # Brass / saffron — the agent, highlights
    "success": (152, 0.140),
    "warning": (58, 0.150),
    "danger": (25, 0.170),
    "info": (245, 0.120),
}
NEUTRAL_HUE, NEUTRAL_CHROMA = 75, 0.012  # warm sand-grey


def scale(hue: float, peak: float) -> dict[int, str]:
    return {s: oklch(l, peak * c, hue) for s, l, c in zip(STEPS, LIGHTNESS, CHROMA_CURVE)}


def neutral_scale() -> dict[int, str]:
    lights = {0: 1.0, 25: 0.985, 50: 0.968, 100: 0.940, 200: 0.895, 300: 0.820, 400: 0.700,
              500: 0.585, 600: 0.480, 700: 0.390, 750: 0.330, 800: 0.285, 850: 0.245,
              900: 0.205, 950: 0.165}
    return {s: oklch(l, 0 if s == 0 else NEUTRAL_CHROMA * (0.6 if l < 0.3 else 1), NEUTRAL_HUE)
            for s, l in lights.items()}


PALETTE: dict[str, dict[int, str]] = {name: scale(h, c) for name, (h, c) in HUES.items()}
PALETTE["neutral"] = neutral_scale()

# Eight series / participant colors, in fixed order (charts, avatars, presence cursors).
SERIES_HUES = (183, 72, 245, 25, 152, 300, 205, 340)
SERIES = {
    "light": [oklch(0.56, 0.12, h) for h in SERIES_HUES],
    "dark": [oklch(0.74, 0.11, h) for h in SERIES_HUES],
}
SERIES["dim"] = SERIES["dark"]

# ---------------------------------------------------------------- semantic tokens per theme

P, N = PALETTE, PALETTE["neutral"]


def _status(theme: str) -> dict[str, str]:
    out: dict[str, str] = {}
    for name in ("success", "warning", "danger", "info"):
        s = P[name]
        if theme == "light":
            out |= {f"{name}-solid": s[600], f"{name}-text": s[700], f"{name}-bg": s[50], f"{name}-border": s[200]}
        else:
            bg = oklch(0.30 if theme == "dark" else 0.36, HUES[name][1] * 0.35, HUES[name][0])
            out |= {f"{name}-solid": s[400], f"{name}-text": s[200], f"{name}-bg": bg, f"{name}-border": s[700]}
    return out


THEMES: dict[str, dict[str, str]] = {
    "light": {
        "surface": N[0], "surface-raised": N[0], "surface-sunken": N[50], "surface-overlay": N[0],
        "surface-hover": N[100], "surface-selected": P["brand"][50],
        "text-primary": N[900], "text-secondary": N[700], "text-muted": N[600], "text-disabled": N[400],
        "text-on-brand": "#ffffff", "text-on-accent": N[950],
        "border-subtle": N[200], "border-strong": N[500],
        "brand-solid": P["brand"][600], "brand-solid-hover": P["brand"][700], "brand-text": P["brand"][700],
        "brand-subtle": P["brand"][50], "brand-subtle-text": P["brand"][800],
        "accent-solid": P["accent"][400], "accent-solid-hover": P["accent"][300], "accent-text": P["accent"][800],
        "accent-subtle": P["accent"][50],
        "agent": P["accent"][500], "agent-subtle": P["accent"][50], "driver": P["brand"][600],
        "focus-ring": P["brand"][500],
        **_status("light"),
    },
    "dark": {
        "surface": N[900], "surface-raised": N[850], "surface-sunken": N[950], "surface-overlay": N[800],
        "surface-hover": N[800], "surface-selected": oklch(0.30, 0.04, HUES["brand"][0]),
        "text-primary": N[50], "text-secondary": N[300], "text-muted": N[400], "text-disabled": N[600],
        "text-on-brand": N[950], "text-on-accent": N[950],
        "border-subtle": N[750], "border-strong": N[500],
        "brand-solid": P["brand"][400], "brand-solid-hover": P["brand"][300], "brand-text": P["brand"][300],
        "brand-subtle": oklch(0.28, 0.035, HUES["brand"][0]), "brand-subtle-text": P["brand"][200],
        "accent-solid": P["accent"][400], "accent-solid-hover": P["accent"][300], "accent-text": P["accent"][300],
        "accent-subtle": oklch(0.29, 0.045, HUES["accent"][0]),
        "agent": P["accent"][400], "agent-subtle": oklch(0.29, 0.045, HUES["accent"][0]), "driver": P["brand"][400],
        "focus-ring": P["brand"][300],
        **_status("dark"),
    },
    "dim": {
        "surface": N[750], "surface-raised": N[700], "surface-sunken": N[800], "surface-overlay": N[700],
        "surface-hover": N[700], "surface-selected": oklch(0.38, 0.04, HUES["brand"][0]),
        "text-primary": N[25], "text-secondary": N[200], "text-muted": N[300], "text-disabled": N[500],
        "text-on-brand": N[950], "text-on-accent": N[950],
        "border-subtle": N[600], "border-strong": N[400],
        "brand-solid": P["brand"][300], "brand-solid-hover": P["brand"][200], "brand-text": P["brand"][200],
        "brand-subtle": oklch(0.37, 0.035, HUES["brand"][0]), "brand-subtle-text": P["brand"][100],
        "accent-solid": P["accent"][300], "accent-solid-hover": P["accent"][200], "accent-text": P["accent"][200],
        "accent-subtle": oklch(0.38, 0.045, HUES["accent"][0]),
        "agent": P["accent"][300], "agent-subtle": oklch(0.38, 0.045, HUES["accent"][0]), "driver": P["brand"][300],
        "focus-ring": P["brand"][200],
        **_status("dim"),
    },
}

# ---------------------------------------------------------------- WCAG checks

TEXT = 4.5  # body text
UI = 3.0  # large text, icons, borders of controls, focus rings


def checks(theme: str) -> list[tuple[str, str, float]]:
    """(foreground token, background token, minimum ratio) pairs that the UI actually uses."""
    pairs: list[tuple[str, str, float]] = []
    for bg in ("surface", "surface-raised", "surface-sunken", "surface-hover", "surface-selected"):
        for fg in ("text-primary", "text-secondary", "text-muted", "brand-text"):
            pairs.append((fg, bg, TEXT))
        for fg in ("border-strong", "focus-ring", "brand-solid", "agent", "driver"):
            pairs.append((fg, bg, UI))
    pairs += [
        ("text-on-brand", "brand-solid", TEXT), ("text-on-brand", "brand-solid-hover", TEXT),
        ("text-on-accent", "accent-solid", TEXT), ("text-on-accent", "accent-solid-hover", TEXT),
        ("brand-subtle-text", "brand-subtle", TEXT), ("accent-text", "accent-subtle", TEXT),
        ("accent-text", "surface", TEXT), ("text-primary", "agent-subtle", TEXT),
        ("text-primary", "surface-overlay", TEXT), ("text-secondary", "surface-overlay", TEXT),
    ]
    for s in ("success", "warning", "danger", "info"):
        pairs += [(f"{s}-text", f"{s}-bg", TEXT), (f"{s}-text", "surface", TEXT), (f"{s}-solid", "surface", UI)]
    return pairs


def run_checks() -> list[str]:
    failures = []
    for theme, t in THEMES.items():
        for fg, bg, minimum in checks(theme):
            ratio = contrast(t[fg], t[bg])
            if ratio < minimum:
                failures.append(f"{theme}: {fg} on {bg} = {ratio:.2f} < {minimum}")
        for i, c in enumerate(SERIES[theme]):
            ratio = contrast(c, t["surface"])
            if ratio < UI:
                failures.append(f"{theme}: series-{i + 1} on surface = {ratio:.2f} < {UI}")
    return failures


if __name__ == "__main__":
    for name, sc in PALETTE.items():
        print(f"{name:8}", " ".join(sc.values()))
    fails = run_checks()
    print("\n".join(fails) if fails else "All contrast checks pass")
