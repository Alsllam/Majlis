# 0006 — Cloud and on-prem deployment profiles from one codebase

**Status:** Accepted · 2026-10-09

## Context
The product owner decided (SRS OD-1, OD-2) that Majlis starts with private-sector customers in any country and **must support on-premises installations**, connected or air-gapped. The house skills assume Azure services (Azure OpenAI, Azure AI Search, Document Intelligence, Blob Storage, Key Vault, Container Apps) that do not exist inside a customer's data center.

## Decision
One codebase, two **deployment profiles** selected by configuration:

- **`cloud`** — Azure, multi-tenant, one *regional stamp* per supported region; a tenant lives in the stamp of its data region.
- **`on-prem`** — one installation per customer (single tenant), Kubernetes via a Helm chart or one server via Docker Compose; connected or air-gapped.

Every Azure-specific dependency is reached only through an adapter interface, with one implementation per profile:

| Interface | Where | `cloud` | `on-prem` |
|---|---|---|---|
| `IBlobStorage` | backend `Majlis.Framework.Application` | Azure Blob | S3-compatible (MinIO) |
| `IEmailSender`, `IPushSender` | backend Notifications | managed provider, FCM | SMTP; push only when connected |
| `ISecretProvider` (config source) | backend + ai-service | Key Vault + managed identity | Kubernetes secrets / Vault |
| `LlmProvider` (chat, fast, reasoning) | ai-service `llm/` | Azure OpenAI (Responses API) | OpenAI-compatible server (Chat Completions; Responses where the server supports it) |
| `Embedder` | ai-service `llm/` | Azure OpenAI embeddings | self-hosted embedding model |
| `SearchIndex` | ai-service `rag/index` | Azure AI Search | OpenSearch + self-hosted re-ranker |
| `DocumentExtractor` | ai-service `rag/ingestion` | Document Intelligence | DI containers if licensed, else open-source layout + Arabic OCR |
| `Transcriber` | ai-service | Azure OpenAI transcription | self-hosted Whisper-class model |
| `SafetyGuard` | ai-service `safety/` | content filters + Prompt Shields | self-hosted guard model + rules |

SQL Server, Redis, RabbitMQ, SignalR (with the Redis backplane, ADR-0001) and OpenIddict are the same in both profiles.

## Rules
- Business code never imports an Azure SDK directly; only adapter implementations do.
- CI runs the test suite against both adapter sets (on-prem through Docker Compose, which is also the local dev setup).
- The AI eval (AI-EVL-001) must pass on each profile's models before a release ships to that profile; the on-prem model is chosen by the eval.
- Model names, endpoints and regions are configuration, never code (unchanged from the AI skill).

## Alternatives
- **Cloud only, on-prem later** — rejected by the product owner; retrofitting adapters after the code exists is far more expensive.
- **Kubernetes everywhere (also in the cloud)** — more operational work than Container Apps for the SaaS; the Helm chart still exists for on-prem.
- **PostgreSQL + pgvector for search on-prem** — simpler, but weak Arabic full-text analysis compared to OpenSearch's Arabic analyzer.

## Consequences
- Each adapter pair needs tests; capability gaps (e.g. no semantic ranker on-prem) are covered by a self-hosted re-ranker and measured by the eval.
- On-prem customers need GPU capacity for local models; hardware sizing becomes part of the sales process (to be documented with the v1.1 installer).
- The skills' Azure choices still apply to the `cloud` profile; this ADR and the folder `CLAUDE.md` files override them where the `on-prem` profile needs something else.
