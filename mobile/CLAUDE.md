# mobile/ — Majlis mobile (Flutter)

**Rules:** follow `.claude/skills/flutter-clean-mobile/SKILL.md` for everything here. This file gives the Majlis values for its placeholders and wins where they disagree. Product invariants are in the root `CLAUDE.md`.

Mobile comes **after** the web app (see the roadmap in `docs/SRS.md`). `mobile/` is the Flutter project root.

## Placeholder values

| Skill placeholder | Majlis value |
|---|---|
| `{app}` (Dart package) | `majlis` |
| Application id / bundle id | `sa.majlis.app` (**to confirm** with the owner of the final domain) |
| Display name | `مجلس` (ar) / `Majlis` (en) |
| Flavors | `dev`, `staging`, `uat`, `prod` |
| API base URL (dev) | BFF `http://10.0.2.2:7000` (Android emulator) / `http://localhost:7000` (iOS simulator) |
| OAuth client id | `majlis-mobile` (authorization code + PKCE) |
| Brand assets | from `docs/brand/`: `logo/app-icon-*.svg` → launcher icon and splash, `tokens/dart/majlis_tokens.dart` → `AppTokens`, `fonts/*.ttf` → `assets/fonts` |

## Planned features (provisional)

`auth`, `workspaces`, `rooms` (live session view, comments, take over / hand off), `approvals` (approve agent actions from a notification), `knowledge` (browse and upload), `tasks`, `meetings` (record → summary), `notifications`, `settings`.

## Majlis-specific rules

- Approvals are a first-class mobile flow: a push notification deep-links to the approval card, and approve/reject works in two taps with the action diff visible.
- Live sessions use the same real-time protocol as the web (defined in step 4). When offline, show the last known state read-only; never queue an agent instruction or an approval offline.
- Offline sync queue is for user-authored items only (comments drafts, tasks, uploads).
- AI calls go through `/ai-api/**` on the BFF with the user's token. No model keys on device.

## Checks before pushing

```
cd mobile && fvm flutter analyze && dart format --set-exit-if-changed . && fvm flutter test
```
