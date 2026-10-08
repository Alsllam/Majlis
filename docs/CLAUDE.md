# docs/ — Majlis product and architecture docs

No skill applies here. These docs are the source of truth for product decisions; code in the other folders follows them.

## Layout

| Path | Content | Step |
|---|---|---|
| `docs/SRS.md` | Software requirements: vision, personas, roles, functional and AI requirements, NFRs, architecture, data model, MVP and roadmap | 2 |
| `docs/brand/` | Brand kit: logo SVGs (all variants), color tokens (light / dark / dim), font pair, motion tokens | 3 |
| `docs/architecture/` | Architecture docs, starting with real-time collaboration and the backend module list | 4 |
| `docs/adr/NNNN-title.md` | Architecture decision records (context, decision, consequences) | as needed |

## Rules

- Write in English; give Arabic terms next to English ones where the product uses them (e.g. room · غرفة).
- Requirements get stable ids (`FR-ROOM-001`, `NFR-SEC-003`, `AI-RAG-002`) so code, tests and PRs can reference them.
- Diagrams are Mermaid in Markdown, so they render on GitHub and diff cleanly.
- Brand files in `docs/brand/` are the master copies. Web and mobile copy from here; change them here first.
- When a decision changes, update the doc in the same PR as the code.
