# Majlis · مجلس

A shared AI workspace for teams, Arabic-first. Teams work together with the same AI agent session in real time: watch it, redirect it, comment, take over, and hand it to a colleague. The agent answers from the team's own documents with citations, and every action that changes data waits for a human approval.

## Repository

| Folder | Stack |
|---|---|
| [`backend/`](backend/CLAUDE.md) | .NET 9 modular backend, YARP BFF, OpenIddict, MassTransit, SQL Server |
| [`frontend/`](frontend/CLAUDE.md) | Angular 20 in an Nx workspace, ECharts, AntV X6 |
| [`mobile/`](mobile/CLAUDE.md) | Flutter, Clean Architecture, BLoC |
| [`ai-service/`](ai-service/CLAUDE.md) | Python FastAPI, Azure OpenAI, Azure AI Search |
| [`docs/`](docs/CLAUDE.md) | SRS, architecture, ADRs, brand kit |

Engineering rules live in [`CLAUDE.md`](CLAUDE.md), each folder's `CLAUDE.md`, and the skills in [`.claude/skills/`](.claude/skills).
