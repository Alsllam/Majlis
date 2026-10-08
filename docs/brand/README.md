# Majlis brand kit · هوية مجلس

The master copy of the Majlis identity. Web (`frontend/`) and mobile (`mobile/`) copy from here; change things here first, then regenerate. Open `preview.html` through a local server (`python3 -m http.server -d docs/brand`) to see everything in light, dark and dim, in Arabic and English.

## 1. Idea

A *majlis* (مجلس) is the room where people gather to talk and decide together. Seen from above, seating runs along the walls, the entrance stays open, and everyone faces one shared center.

The mark is that floor plan:

- **The room:** one continuous rounded stroke, for the seating along the walls. These are the people, in Majlis teal.
- **The entrance:** the gap at the bottom. The room is open and anyone invited can walk in.
- **The center:** the brass dot, for the shared agent that everyone in the room works with. The brass comes from the *dallah*, the coffee pot that is always at the center of a majlis.

The rounded square with an open side also reads as a speech bubble, because a majlis is a conversation.

**Color meaning, used across the product:** teal stands for people, the brand and actions. Brass (`--agent`) stands for the AI agent and appears only where the agent speaks or acts: its turns, its thinking indicator and its proposals.

## 2. Logo files (`logo/`)

| File | Use |
|---|---|
| `logo-ar.svg` | primary lockup in Arabic UI. The mark sits on the right (the start side in RTL) |
| `logo-en.svg` | lockup in English UI. The mark sits on the left |
| `logo.svg` | bilingual lockup (Arabic over English) for language-neutral places: documents, email footers, signage, store listings |
| `logo-mark.svg` | icon only: app bars, avatars for the agent, small spaces |
| `*-dark.svg` | the same variants tuned for dark surfaces (lighter teal and brass) |
| `*-mono.svg` | single color in `currentColor`, for print, embossing, one-color surfaces and the brand-teal background |
| `app-icon.svg` | full-bleed tile (1024) for iOS, PWA maskable icons and store art. The mark stays inside the 66% safe zone |
| `app-icon-foreground.svg`, `app-icon-background.svg`, `app-icon-monochrome.svg` | Android adaptive icon layers (108 dp) and the themed monochrome icon |

The wordmarks are set in IBM Plex Sans Arabic Bold (مجلس) and IBM Plex Sans SemiBold (Majlis), shaped with HarfBuzz and **converted to outlines**. They render the same everywhere without the fonts installed. Don't retype them.

**Favicons (`favicon/`):**
- `favicon.svg` switches colors with the browser's color scheme.
- `favicon.ico` (16/32/48) and the PNG icons use the teal tile, so they read on any tab bar.
- `site.webmanifest` defaults to Arabic and RTL.

### Rules

- **Clear space:** at least the dot's diameter on every side (14 units on the 64-unit mark).
- **Minimum size:** the mark at 16 px. The lockups at 24 px tall on screen and 8 mm in print.
- **Backgrounds:** use the color files on white, sand (`--neutral-50`) and light photos, the `-dark` files on dark surfaces, and the `-mono` files in white on brand teal.
- **Entrance animation:** on the login and splash screens, the room draws in with a stroke animation (`--motion-logo`, 800 ms), then the dot pops in. It plays once and never loops on working screens. With reduced motion it is a plain fade.
- **Don't:**
  - rotate the mark;
  - close the entrance;
  - move the dot off-center;
  - recolor the dot teal (it is always the agent color, or the single color in mono);
  - add shadows or gradients;
  - set the wordmark in another font;
  - put the mark inside another shape (use `app-icon.svg` when a tile is needed).

## 3. Color (`tokens/`)

All colors come from `tools/palette.py`. Each scale is generated in OKLCH from one hue, so all ten steps are evenly spaced, and every pair the UI uses is checked against WCAG AA (4.5:1 for text, 3:1 for UI and large text). **The build fails if any pair falls short.**

| Scale | Hue | Role |
|---|---|---|
| `brand` (Nakhla teal) | 183° | primary actions, links, focus, the driver indicator |
| `accent` (brass) | 72° | the agent, highlights, secondary call to action |
| `neutral` (warm sand) | 75°, low chroma | surfaces, text, borders. Warm rather than grey, to fit the room |
| `success` / `warning` / `danger` / `info` | 152° / 58° / 25° / 245° | status tags, alerts, charts |

Each scale has steps `50…900`. Neutral also has `0, 25, 750, 850, 950`, used for the dark and dim surfaces.

**Semantic tokens.** Components use only these, never raw hex values or palette steps. They are defined for **light**, **dark** and **dim**, set with `data-theme` on `<html>`:

| Group | Tokens |
|---|---|
| Surfaces | `--surface`, `--surface-raised`, `--surface-sunken`, `--surface-overlay`, `--surface-hover`, `--surface-selected` |
| Text | `--text-primary`, `--text-secondary`, `--text-muted`, `--text-disabled`, `--text-on-brand`, `--text-on-accent` |
| Borders | `--border-subtle`, `--border-strong`, `--focus-ring` |
| Brand | `--brand-solid`, `--brand-solid-hover`, `--brand-text`, `--brand-subtle`, `--brand-subtle-text` |
| Accent / agent | `--accent-solid`, `--accent-solid-hover`, `--accent-text`, `--accent-subtle`, `--agent`, `--agent-subtle` |
| Collaboration | `--driver` (ring around the avatar of the person in control), `--series-1…8` (participant avatars and cursors, chart series, in a fixed order) |
| Status | `--{success,warning,danger,info}-{solid,text,bg,border}` |
| Elevation | `--shadow-sm/md/lg`: teal-tinted and soft in light themes, darker in dark themes |

**Dark is not inverted.** Surfaces get lighter as they rise (sunken → surface → raised → overlay), brand colors move to lighter, less saturated steps, and borders have lower contrast. **Dim** is a softer dark for long sessions and evening use.

## 4. Typography (`fonts/`, `tokens/css/fonts.css`)

| | Arabic | Latin |
|---|---|---|
| Family | **IBM Plex Sans Arabic** | **IBM Plex Sans** |
| Weights shipped | 400, 500, 600, 700 | 400, 500, 600, 700 |
| Why | built as one superfamily with Plex Sans, so the two share proportions, weights and tone. Clear Naskh-based shapes that stay readable in long legal and policy texts. Open source (SIL OFL 1.1), so it can be self-hosted in KSA and bundled in the apps | |

On the web, both are served as **one family, `Majlis Sans`**, split by `unicode-range`. Arabic letters come from Plex Sans Arabic (with `size-adjust: 106%` so both scripts look the same size on mixed lines), and everything else comes from Plex Sans. Mixed sentences such as "راجع البند 4 من NDA" need no extra markup.

- **Size scale:** `--fs-xs 12 · sm 14 · md 16 · lg 18 · xl 20 · 2xl 24 · 3xl 30 · 4xl 36`.
- **Line height:** 1.5 for English body text and 1.25 for headings. Under `:lang(ar)` it is **1.7** for body and **1.4** for headings, because Arabic needs room for its ascenders, descenders and dots.
- **Numbers:** tables and counters use `tabular-nums`. Digit style (Arabic-Indic ٠١٢ or Latin 012) follows the user's preference, not the UI language.
- **Monospace:** the system mono stack, for code and ids only.
- **Mobile:** use the `.ttf` files, picking the family by locale in the Flutter theme.

## 5. Motion (`tokens/css/motion.css`)

Motion explains what changed and who did it. It never decorates.

| Token | ms | Use |
|---|---|---|
| `--motion-instant` | 80 | tooltips, checkbox ticks |
| `--motion-fast` | 120 | hover, focus, press |
| `--motion-base` | 200 | dropdowns, tabs, toasts, filter chips, avatars joining or leaving the room |
| `--motion-slow` | 320 | modals, side panels, page enter, the control hand-off banner |
| `--motion-deliberate` | 480 | an approval card resolving (approved ✓ / rejected), a session summary revealing |
| `--motion-logo` | 800 | logo entrance, once |
| `--motion-pulse` | 1600 | one cycle of the "live" presence dot and the agent-thinking shimmer |
| `--motion-stagger` | 30 | list item stagger, first 10 items only |

| Easing | Value | Use |
|---|---|---|
| `--ease-out` | `cubic-bezier(.2,.8,.2,1)` | elements entering |
| `--ease-in` | `cubic-bezier(.4,0,1,1)` | elements leaving |
| `--ease-in-out` | `cubic-bezier(.65,0,.35,1)` | things moving between places, e.g. the driver ring passing from one avatar to another |
| `--ease-emphasized` | `cubic-bezier(.3,1.4,.5,1)` | small overshoot for confirmations (approval ✓, reaction pop). Never on layout |

Distances and scales: page enter rises **8 px**, buttons lift **2 px** on hover, press scales to **0.98**, pop to **1.06**.

**Shared keyframes:** `majlis-enter`, `majlis-fade`, `majlis-pulse`, `majlis-pop`, `majlis-shimmer`, `majlis-draw`.

**Majlis-specific patterns:**

- **Agent streaming:** text appears as it arrives, with no typewriter effect added on top. While the agent searches or thinks, a brass shimmer line runs at `--motion-pulse`.
- **Presence:** avatars slide into the stack (`base`). The live dot pulses slowly, and only on "active now" indicators.
- **Control hand-off:** the driver ring moves from the old driver's avatar to the new one (`slow`, `ease-in-out`), and a banner says who is now in control.
- **Approvals:** a new approval card enters with `majlis-enter`. Approving pops a ✓ (`emphasized`), then the card settles into its resolved state (`deliberate`).

**Reduced motion:** `prefers-reduced-motion: reduce` sets stagger, pulse, distances and scales to zero or no-op in the tokens themselves. Components that use the tokens get this behaviour for free, and only opacity changes remain.

## 6. Voice (short)

- **Arabic first:** formal Modern Standard Arabic, clear and warm, never stiff. In English: plain, direct, calm.
- **The agent is "الوكيل" / "the agent",** never a human name. It says what it did and what it needs from you ("أحتاج اعتمادك لإنشاء ٣ مهام" · "I need your approval to create 3 tasks").
- **Say who did what:** "Sara handed control to Khalid", "the agent proposed…", "Noura approved…".

## 7. Files and how to rebuild

```
docs/brand/
├── logo/          logo variants, app icon layers
├── favicon/       favicon.svg, favicon.ico, apple-touch-icon.png, manifest icons, site.webmanifest
├── fonts/         IBM Plex Sans Arabic + IBM Plex Sans (.ttf for mobile, .woff2 for web), OFL.txt
├── tokens/
│   ├── tokens.json                 everything, machine-readable
│   ├── css/index.css               imports fonts, colors (light/dark/dim), typography, motion
│   ├── scss/_majlis-tokens.scss    maps for the frontend theme-layout-generator
│   ├── dart/majlis_tokens.dart     constants for the Flutter ThemeExtension
│   └── echarts/majlis-{light,dark,dim}.json   ECharts themes
├── tools/         palette.py (colors + WCAG checks), build.py (generates everything above)
└── preview.html   visual check of the whole kit
```

To rebuild after changing `palette.py`, `build.py` or the fonts:

```
python3 -m venv .venv && .venv/bin/pip install -r docs/brand/tools/requirements.txt
.venv/bin/python docs/brand/tools/build.py      # needs headless Chromium for the PNG icons
```

**How the apps use the kit:**

- **Web:** `frontend/` copies `logo/` and `favicon/` into `apps/web/src/assets/brand/`, the woff2 fonts into `assets/fonts/`, and builds its theme from `scss/_majlis-tokens.scss`. It registers `echarts/*.json` as the `majlis-*` chart themes.
- **Mobile:** `mobile/` wraps `dart/majlis_tokens.dart` in its `AppTokens` theme extension, bundles the `.ttf` fonts, and builds the launcher icon and splash from `app-icon-*.svg`.

**Fonts:** IBM Plex Sans Arabic and IBM Plex Sans © IBM Corp., licensed under the SIL Open Font License 1.1 (`fonts/OFL.txt`). Majlis logo and mark: original artwork for this product.
