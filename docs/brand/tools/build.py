"""Build the Majlis brand kit.

Generates, from `palette.py` and the font files:
  logo/       every logo variant (wordmarks are outlined from the brand fonts, so they render
              identically everywhere without the fonts installed)
  favicon/    favicon.svg, favicon.ico, apple-touch-icon.png, manifest icons, site.webmanifest
  tokens/     tokens.json, css/*.css, scss/_majlis-tokens.scss, dart/majlis_tokens.dart, echarts/*.json
  fonts/      woff2 copies of the TTF files

Run from the repo root:
  python -m venv .venv && .venv/bin/pip install -r docs/brand/tools/requirements.txt
  .venv/bin/python docs/brand/tools/build.py
PNG rendering uses headless Chromium (set CHROME=/path/to/chrome if it is not found).
"""

from __future__ import annotations

import glob
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import uharfbuzz as hb
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
from palette import HUES, PALETTE, SERIES, THEMES, run_checks  # noqa: E402

BRAND = Path(__file__).resolve().parent.parent
FONTS = BRAND / "fonts"
LOGO = BRAND / "logo"
FAV = BRAND / "favicon"
TOK = BRAND / "tokens"

# ---------------------------------------------------------------- mark geometry (64 × 64 grid)
#
# A majlis seen from above: seating along the walls of a room (the rounded square), the
# entrance left open at the bottom, and everyone gathered around one shared center — the agent
# (the brass dot). Stroke 8, corner radius 12, entrance gap 16, dot radius 7.

ROOM = "M24 52H22a12 12 0 0 1-12-12V24a12 12 0 0 1 12-12h20a12 12 0 0 1 12 12v16a12 12 0 0 1-12 12h-2"
STROKE = 8
DOT = (32, 32, 7)

COLORS = {
    # variant: (room color, dot color)
    "color": (PALETTE["brand"][600], PALETTE["accent"][400]),
    "dark": (PALETTE["brand"][300], PALETTE["accent"][300]),
    "mono": ("currentColor", "currentColor"),
}
WORDMARK_COLOR = {"color": PALETTE["neutral"][900], "dark": PALETTE["neutral"][25], "mono": "currentColor"}


def mark_group(room: str, dot: str, dx: float = 0, dy: float = 0, scale: float = 1) -> str:
    cx, cy, r = DOT
    return (
        f'<g transform="translate({dx:g} {dy:g}) scale({scale:g})">'
        f'<path d="{ROOM}" fill="none" stroke="{room}" stroke-width="{STROKE}" stroke-linecap="round"/>'
        f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{dot}"/></g>'
    )


def svg(width: float, height: float, body: str, title: str) -> str:
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:g} {height:g}" '
        f'width="{width:g}" height="{height:g}" role="img" aria-label="{title}">'
        f"<title>{title}</title>{body}</svg>\n"
    )


# ---------------------------------------------------------------- outlined wordmarks


def outline(text: str, font_path: Path, size: float, tracking: float = 0.0) -> tuple[str, float, float, float]:
    """Shape `text` with HarfBuzz and return (svg path d, advance width, top, bottom) at `size` units.

    The baseline is y = 0; top is negative (SVG y grows down).
    """
    blob = hb.Blob.from_file_path(str(font_path))
    face = hb.Face(blob)
    font = hb.Font(face)
    buf = hb.Buffer()
    buf.add_str(text)
    buf.guess_segment_properties()
    hb.shape(font, buf, {"kern": True, "liga": True})

    tt = TTFont(str(font_path))
    glyph_set = tt.getGlyphSet()
    order = tt.getGlyphOrder()
    upm = tt["head"].unitsPerEm
    k = size / upm

    pen = SVGPathPen(glyph_set)
    x = 0.0
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        name = order[info.codepoint]
        tp = TransformPen(pen, (k, 0, 0, -k, (x + pos.x_offset) * k, -pos.y_offset * k))
        glyph_set[name].draw(tp)
        x += pos.x_advance + tracking * upm
    x -= tracking * upm

    # bounds from the outlines
    from fontTools.pens.boundsPen import BoundsPen

    bp = BoundsPen(glyph_set)
    x2 = 0.0
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        tp = TransformPen(bp, (1, 0, 0, 1, x2 + pos.x_offset, pos.y_offset))
        glyph_set[order[info.codepoint]].draw(tp)
        x2 += pos.x_advance + tracking * upm
    _, ymin, _, ymax = bp.bounds
    return pen.getCommands(), x * k, -ymax * k, -ymin * k


AR_FONT = FONTS / "IBMPlexSansArabic-Bold.ttf"
EN_FONT = FONTS / "IBMPlexSans-SemiBold.ttf"
AR_SIZE, EN_SIZE = 44, 38  # tuned so both wordmarks sit on the mark with the same visual weight
GAP = 14  # mark ↔ wordmark


def build_logos() -> None:
    LOGO.mkdir(exist_ok=True)
    ar_d, ar_w, ar_top, ar_bot = outline("مجلس", AR_FONT, AR_SIZE)
    en_d, en_w, en_top, en_bot = outline("Majlis", EN_FONT, EN_SIZE, tracking=-0.01)
    small_ar_d, small_ar_w, sar_top, sar_bot = outline("مجلس", AR_FONT, 30)
    small_en_d, small_en_w, sen_top, sen_bot = outline("Majlis", EN_FONT, 22, tracking=0.0)

    for variant, (room, dot) in COLORS.items():
        suffix = "" if variant == "color" else f"-{variant}"
        word = WORDMARK_COLOR[variant]
        mark = mark_group(room, dot)

        # icon only
        (LOGO / f"logo-mark{suffix}.svg").write_text(svg(64, 64, mark, "Majlis"))

        # English lockup: mark at the start (left), wordmark after it, baseline aligned to the room's lower edge
        base = 46
        w = 64 + GAP + en_w + 2
        body = mark + f'<path transform="translate({64 + GAP:g} {base:g})" d="{en_d}" fill="{word}"/>'
        (LOGO / f"logo-en{suffix}.svg").write_text(svg(round(w, 1), 64, body, "Majlis"))

        # Arabic lockup: mark at the start (right), wordmark to its left
        base_ar = 42
        w = ar_w + GAP + 64 + 2
        body = (
            f'<path transform="translate(2 {base_ar:g})" d="{ar_d}" fill="{word}"/>'
            + mark_group(room, dot, dx=2 + ar_w + GAP)
        )
        h = max(64, base_ar + ar_bot + 2)
        (LOGO / f"logo-ar{suffix}.svg").write_text(svg(round(w, 1), round(h, 1), body, "مجلس"))

        # Bilingual lockup: mark + Arabic over English (language-neutral surfaces, documents, signage)
        text_w = max(small_ar_w, small_en_w)
        w = 64 + GAP + text_w + 2
        body = (
            mark
            + f'<path transform="translate({64 + GAP:g} {30:g})" d="{small_ar_d}" fill="{word}"/>'
            + f'<path transform="translate({64 + GAP + 1:g} {56:g})" d="{small_en_d}" fill="{word}"/>'
        )
        (LOGO / f"logo{suffix}.svg").write_text(svg(round(w, 1), 64, body, "مجلس Majlis"))

    # App icon: full-bleed tile (iOS, PWA maskable, store listings). The mark sits in the 66% safe zone.
    tile = PALETTE["brand"][600]
    body = f'<rect width="1024" height="1024" fill="{tile}"/>' + mark_group("#ffffff", PALETTE["accent"][300], 192, 192, 10)
    (LOGO / "app-icon.svg").write_text(svg(1024, 1024, body, "Majlis"))
    # Android adaptive icon layers (108 dp canvas, 72 dp visible → mark inside the inner 66 dp)
    (LOGO / "app-icon-background.svg").write_text(svg(108, 108, f'<rect width="108" height="108" fill="{tile}"/>', "Majlis background"))
    fg = mark_group("#ffffff", PALETTE["accent"][300], 27, 27, 54 / 64)
    (LOGO / "app-icon-foreground.svg").write_text(svg(108, 108, fg, "Majlis foreground"))
    (LOGO / "app-icon-monochrome.svg").write_text(svg(108, 108, mark_group("#000000", "#000000", 27, 27, 54 / 64), "Majlis monochrome"))


# ---------------------------------------------------------------- favicons


def find_chrome() -> str:
    for c in [os.environ.get("CHROME", ""), *glob.glob("/opt/pw-browsers/chromium-*/chrome-linux/chrome"),
              shutil.which("chromium") or "", shutil.which("google-chrome") or ""]:
        if c and Path(c).exists():
            return c
    sys.exit("Chromium not found; set CHROME=/path/to/chrome")


def render_png(svg_text: str, size: int) -> Image.Image:
    with tempfile.TemporaryDirectory() as tmp:
        page = Path(tmp) / "i.html"
        page.write_text(
            "<html><body style='margin:0;background:transparent'>"
            f"<img src='data:image/svg+xml;base64,{__import__('base64').b64encode(svg_text.encode()).decode()}' "
            f"width='{size}' height='{size}' style='display:block'></body></html>"
        )
        out = Path(tmp) / "o.png"
        subprocess.run(
            [find_chrome(), "--headless", "--no-sandbox", "--disable-gpu", "--hide-scrollbars",
             "--force-device-scale-factor=1", "--default-background-color=00000000",
             f"--window-size={max(size, 600)},{max(size, 600)}", f"--screenshot={out}", page.as_uri()],
            check=True, capture_output=True,
        )
        return Image.open(io.BytesIO(out.read_bytes())).convert("RGBA").crop((0, 0, size, size))


def build_favicons() -> None:
    FAV.mkdir(exist_ok=True)
    light_room, light_dot = COLORS["color"]
    dark_room, dark_dot = COLORS["dark"]
    fav = (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'
        f"<style>.r{{stroke:{light_room}}}.d{{fill:{light_dot}}}"
        f"@media (prefers-color-scheme:dark){{.r{{stroke:{dark_room}}}.d{{fill:{dark_dot}}}}}</style>"
        f'<path class="r" d="{ROOM}" fill="none" stroke-width="{STROKE + 1}" stroke-linecap="round"/>'
        f'<circle class="d" cx="{DOT[0]}" cy="{DOT[1]}" r="{DOT[2] + 0.5}"/></svg>\n'
    )
    (FAV / "favicon.svg").write_text(fav)

    tile_svg = (LOGO / "app-icon.svg").read_text()
    # rounded tile for small sizes so it reads on both light and dark tab bars
    small_tile = svg(64, 64, f'<rect width="64" height="64" rx="14" fill="{PALETTE["brand"][600]}"/>'
                     + mark_group("#ffffff", PALETTE["accent"][300], 6, 6, 52 / 64), "Majlis")
    ico_images = [render_png(small_tile, s) for s in (16, 32, 48)]
    ico_images[-1].save(FAV / "favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)], append_images=ico_images[:-1])
    render_png(tile_svg, 180).convert("RGB").save(FAV / "apple-touch-icon.png")
    render_png(tile_svg, 192).save(FAV / "web-app-manifest-192x192.png")
    render_png(tile_svg, 512).save(FAV / "web-app-manifest-512x512.png")
    manifest = {
        "name": "Majlis · مجلس",
        "short_name": "مجلس",
        "lang": "ar",
        "dir": "rtl",
        "start_url": "/",
        "display": "standalone",
        "theme_color": PALETTE["brand"][600],
        "background_color": THEMES["light"]["surface-sunken"],
        "icons": [
            {"src": "web-app-manifest-192x192.png", "sizes": "192x192", "type": "image/png", "purpose": "maskable"},
            {"src": "web-app-manifest-512x512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable"},
            {"src": "favicon.svg", "sizes": "any", "type": "image/svg+xml"},
        ],
    }
    (FAV / "site.webmanifest").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


# ---------------------------------------------------------------- tokens

TYPE = {
    "family": {
        "sans": "'Majlis Sans', 'IBM Plex Sans Arabic', 'IBM Plex Sans', system-ui, sans-serif",
        "arabic": "'IBM Plex Sans Arabic', system-ui, sans-serif",
        "latin": "'IBM Plex Sans', system-ui, sans-serif",
        "mono": "ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace",
    },
    "size": {"xs": 12, "sm": 14, "md": 16, "lg": 18, "xl": 20, "2xl": 24, "3xl": 30, "4xl": 36},
    "weight": {"regular": 400, "medium": 500, "semibold": 600, "bold": 700},
    "lineHeight": {"body": 1.5, "bodyAr": 1.7, "heading": 1.25, "headingAr": 1.4},
}

MOTION = {
    "duration": {
        "instant": 80,     # tooltips, checkbox ticks
        "fast": 120,       # hover, focus, press
        "base": 200,       # dropdowns, tabs, toasts, filter chips, presence avatars joining/leaving
        "slow": 320,       # modals, side panels, page transitions, control hand-off banner
        "deliberate": 480, # approval card resolving (approved / rejected), session summary reveal
        "logo": 800,       # logo entrance on login/splash, once
        "pulse": 1600,     # "live" presence dot and agent-thinking indicator, one cycle
        "stagger": 30,     # list item stagger step (first 10 items max)
    },
    "easing": {
        "out": "cubic-bezier(.2,.8,.2,1)",       # entering
        "in": "cubic-bezier(.4,0,1,1)",          # leaving
        "inOut": "cubic-bezier(.65,0,.35,1)",    # moving / morphing (control token passing between avatars)
        "emphasized": "cubic-bezier(.3,1.4,.5,1)",  # small overshoot: approval ✓, reaction pop (never on layout)
    },
    "distance": {"enter": 8, "lift": 2},  # px: page/route enter rise, button hover lift
    "scale": {"press": 0.98, "pop": 1.06},
}

SPACE = {"1": 4, "2": 8, "3": 12, "4": 16, "5": 24, "6": 32, "7": 48, "8": 64}
RADIUS = {"sm": 6, "md": 8, "lg": 12, "xl": 16, "full": 9999}


def shadows(theme: str) -> dict[str, str]:
    if theme == "light":
        tint = "15 60 55"  # brand-tinted, soft and layered
        return {
            "sm": f"0 1px 2px rgb({tint} / .06), 0 1px 3px rgb({tint} / .08)",
            "md": f"0 2px 4px rgb({tint} / .06), 0 6px 12px rgb({tint} / .08)",
            "lg": f"0 4px 8px rgb({tint} / .06), 0 16px 32px rgb({tint} / .12)",
        }
    return {
        "sm": "0 1px 2px rgb(0 0 0 / .35)",
        "md": "0 2px 4px rgb(0 0 0 / .30), 0 6px 14px rgb(0 0 0 / .30)",
        "lg": "0 4px 8px rgb(0 0 0 / .30), 0 18px 36px rgb(0 0 0 / .40)",
    }


def build_tokens() -> None:
    for d in ("css", "scss", "dart", "echarts"):
        (TOK / d).mkdir(parents=True, exist_ok=True)

    tokens = {
        "palette": {name: {str(k): v for k, v in sc.items()} for name, sc in PALETTE.items()},
        "themes": {t: {**THEMES[t], **{f"shadow-{k}": v for k, v in shadows(t).items()}} for t in THEMES},
        "series": SERIES,
        "typography": TYPE,
        "motion": MOTION,
        "space": SPACE,
        "radius": RADIUS,
        "hues": {k: {"hue": h, "chroma": c} for k, (h, c) in HUES.items()},
    }
    (TOK / "tokens.json").write_text(json.dumps(tokens, ensure_ascii=False, indent=2) + "\n")

    header = "/* Generated by docs/brand/tools/build.py — do not edit by hand. */\n"

    # colors.css: palette on :root, semantic tokens per theme ([data-theme] on <html>)
    lines = [header, ":root {"]
    for name, sc in PALETTE.items():
        lines += [f"  --{name}-{k}: {v};" for k, v in sc.items()]
    lines.append("}\n")
    for theme in THEMES:
        sel = ':root,\n[data-theme="light"]' if theme == "light" else f'[data-theme="{theme}"]'
        lines.append(f"{sel} {{")
        lines.append(f"  color-scheme: {'light' if theme == 'light' else 'dark'};")
        lines += [f"  --{k}: {v};" for k, v in THEMES[theme].items()]
        lines += [f"  --shadow-{k}: {v};" for k, v in shadows(theme).items()]
        lines += [f"  --series-{i + 1}: {c};" for i, c in enumerate(SERIES[theme])]
        lines.append("}\n")
    (TOK / "css" / "colors.css").write_text("\n".join(lines))

    # typography.css
    t = TYPE
    css = [header, ":root {"]
    css += [f"  --font-{k}: {v};" for k, v in t["family"].items()]
    css += [f"  --fs-{k}: {v / 16:g}rem;" for k, v in t["size"].items()]
    css += [f"  --fw-{k}: {v};" for k, v in t["weight"].items()]
    css += ["  --lh-body: 1.5;", "  --lh-heading: 1.25;", "}", "",
            ":lang(ar) {", "  --lh-body: 1.7;", "  --lh-heading: 1.4;", "}", "",
            "body {", "  font-family: var(--font-sans);", "  font-size: var(--fs-md);",
            "  line-height: var(--lh-body);", "  color: var(--text-primary);", "  background: var(--surface-sunken);", "}", "",
            "h1, h2, h3, h4 { line-height: var(--lh-heading); font-weight: var(--fw-semibold); }", "",
            ".tabular, td, .num { font-variant-numeric: tabular-nums; }", ""]
    (TOK / "css" / "typography.css").write_text("\n".join(css))

    # fonts.css: one family name; Arabic glyphs come from Plex Sans Arabic, everything else from Plex Sans
    arabic_range = "U+0600-06FF, U+0750-077F, U+0870-08FF, U+FB50-FDFF, U+FE70-FEFF, U+200C-200F, U+061C"
    faces = [header]
    for weight, name in ((400, "Regular"), (500, "Medium"), (600, "SemiBold"), (700, "Bold")):
        faces.append(
            "@font-face {\n  font-family: 'Majlis Sans';\n"
            f"  src: url('../../fonts/IBMPlexSansArabic-{name}.woff2') format('woff2');\n"
            f"  font-weight: {weight};\n  font-display: swap;\n  unicode-range: {arabic_range};\n"
            "  size-adjust: 106%;\n}\n"
        )
        faces.append(
            "@font-face {\n  font-family: 'Majlis Sans';\n"
            f"  src: url('../../fonts/IBMPlexSans-{name}.woff2') format('woff2');\n"
            f"  font-weight: {weight};\n  font-display: swap;\n}}\n"
        )
    (TOK / "css" / "fonts.css").write_text("\n".join(faces))

    # motion.css
    m = MOTION
    css = [header, ":root {"]
    css += [f"  --motion-{k}: {v}ms;" for k, v in m["duration"].items()]
    css += [f"  --ease-{k.replace('inOut', 'in-out')}: {v};" for k, v in m["easing"].items()]
    css += [f"  --motion-distance-{k}: {v}px;" for k, v in m["distance"].items()]
    css += [f"  --motion-scale-{k}: {v};" for k, v in m["scale"].items()]
    css += [f"  --space-{k}: {v}px;" for k, v in SPACE.items()]
    css += [f"  --radius-{k}: {v}px;" for k, v in RADIUS.items()]
    css += ["}", "",
            "@media (prefers-reduced-motion: reduce) {",
            "  :root {",
            "    --motion-stagger: 0ms;",
            "    --motion-pulse: 0ms;",
            "    --motion-distance-enter: 0px;",
            "    --motion-distance-lift: 0px;",
            "    --motion-scale-press: 1;",
            "    --motion-scale-pop: 1;",
            "    --ease-emphasized: var(--ease-out);",
            "  }",
            "}", "",
            "/* Shared keyframes */",
            "@keyframes majlis-enter { from { opacity: 0; transform: translateY(var(--motion-distance-enter)); } }",
            "@keyframes majlis-fade { from { opacity: 0; } }",
            "@keyframes majlis-pulse { 0%, 100% { opacity: 1; transform: scale(1); } 50% { opacity: .45; transform: scale(.85); } }",
            "@keyframes majlis-pop { 0% { transform: scale(.6); opacity: 0; } 70% { transform: scale(var(--motion-scale-pop)); opacity: 1; } 100% { transform: scale(1); } }",
            "@keyframes majlis-shimmer { from { background-position: 100% 0; } to { background-position: -100% 0; } }",
            "@keyframes majlis-draw { from { stroke-dashoffset: var(--len, 160); } to { stroke-dashoffset: 0; } }", ""]
    (TOK / "css" / "motion.css").write_text("\n".join(css))
    (TOK / "css" / "index.css").write_text(
        header + "@import './fonts.css';\n@import './colors.css';\n@import './typography.css';\n@import './motion.css';\n"
    )

    # SCSS for the frontend theme-layout-generator
    scss = [header.replace("/*", "//").replace(" */", ""), "$majlis-palette: ("]
    for name, sc in PALETTE.items():
        scss.append(f"  '{name}': (" + ", ".join(f"{k}: {v}" for k, v in sc.items()) + "),")
    scss.append(");\n")
    for theme in THEMES:
        scss.append(f"$majlis-{theme}: (")
        scss += [f"  '{k}': {v}," for k, v in THEMES[theme].items()]
        scss.append(");\n")
    (TOK / "scss" / "_majlis-tokens.scss").write_text("\n".join(scss))

    # Dart constants for the Flutter ThemeExtension (mobile/lib/app/theme)
    def dart_color(hex_color: str) -> str:
        return f"Color(0xFF{hex_color[1:].upper()})"

    def camel(s: str) -> str:
        head, *rest = s.split("-")
        return head + "".join(p.capitalize() for p in rest)

    dart = ["// Generated by docs/brand/tools/build.py — do not edit by hand.", "// ignore_for_file: public_member_api_docs",
            "import 'dart:ui';", "", "abstract final class MajlisPalette {"]
    for name, sc in PALETTE.items():
        dart += [f"  static const {name}{k} = {dart_color(v)};" for k, v in sc.items()]
    dart += ["}", ""]
    for theme in THEMES:
        dart.append(f"abstract final class Majlis{theme.capitalize()}Colors {{")
        dart += [f"  static const {camel(k)} = {dart_color(v)};" for k, v in THEMES[theme].items()]
        dart.append(f"  static const series = <Color>[{', '.join(dart_color(c) for c in SERIES[theme])}];")
        dart += ["}", ""]
    dart.append("abstract final class MajlisMotion {")
    dart += [f"  static const {k} = Duration(milliseconds: {v});" for k, v in MOTION["duration"].items()]
    dart += ["}", ""]
    (TOK / "dart" / "majlis_tokens.dart").write_text("\n".join(dart))

    # ECharts themes
    for theme in THEMES:
        tk = THEMES[theme]
        echarts = {
            "color": SERIES[theme],
            "backgroundColor": "transparent",
            "textStyle": {"fontFamily": TYPE["family"]["sans"], "color": tk["text-secondary"]},
            "title": {"textStyle": {"color": tk["text-primary"]}, "subtextStyle": {"color": tk["text-muted"]}},
            "legend": {"textStyle": {"color": tk["text-secondary"]}},
            "tooltip": {"backgroundColor": tk["surface-overlay"], "borderColor": tk["border-subtle"],
                        "textStyle": {"color": tk["text-primary"]}},
            "categoryAxis": {"axisLine": {"lineStyle": {"color": tk["border-strong"]}},
                             "axisLabel": {"color": tk["text-muted"]}, "splitLine": {"show": False}},
            "valueAxis": {"axisLine": {"show": False}, "axisLabel": {"color": tk["text-muted"]},
                          "splitLine": {"lineStyle": {"color": tk["border-subtle"]}}},
            "animationDuration": 600,
            "animationEasing": "cubicOut",
        }
        (TOK / "echarts" / f"majlis-{theme}.json").write_text(json.dumps(echarts, indent=2) + "\n")


def build_fonts() -> None:
    for ttf in sorted(FONTS.glob("*.ttf")):
        f = TTFont(str(ttf))
        f.flavor = "woff2"
        f.save(str(ttf.with_suffix(".woff2")))


def main() -> None:
    failures = run_checks()
    if failures:
        sys.exit("Contrast checks failed:\n" + "\n".join(failures))
    build_fonts()
    build_tokens()
    build_logos()
    build_favicons()
    print("Brand kit built:", BRAND)


if __name__ == "__main__":
    main()
