"""Cloud adapter: Azure AI Search, hybrid (BM25 + vector) with the semantic ranker. The only module importing it."""

from collections.abc import Sequence
from typing import Any
from uuid import UUID

from azure.core.credentials import AzureKeyCredential
from azure.identity.aio import DefaultAzureCredential
from azure.search.documents.aio import SearchClient
from azure.search.documents.indexes.aio import SearchIndexClient
from azure.search.documents.indexes.models import (
    HnswAlgorithmConfiguration,
    SearchableField,
    SearchField,
    SearchIndex,
    SemanticConfiguration,
    SemanticField,
    SemanticPrioritizedFields,
    SemanticSearch,
    SimpleField,
    VectorSearch,
    VectorSearchProfile,
)
from azure.search.documents.models import VectorizedQuery, VectorQuery

from ai_service.rag.models import IndexedChunk, SearchHit
from ai_service.settings import Settings

SEMANTIC_CONFIG = "majlis-semantic"
VECTOR_PROFILE = "majlis-hnsw"


def index_definition(name: str, dimensions: int) -> SearchIndex:
    """Skill §5 schema: Arabic and English analyzers, normalized search copy, HNSW vectors, semantic configuration."""
    return SearchIndex(
        name=name,
        fields=[
            SimpleField(name="id", type="Edm.String", key=True),
            SearchableField(name="content", type="Edm.String", analyzer_name="ar.microsoft"),
            SearchableField(name="content_en", type="Edm.String", analyzer_name="en.microsoft"),
            SearchableField(name="content_search", type="Edm.String", analyzer_name="standard.lucene"),
            SearchField(
                name="content_vector",
                type="Collection(Edm.Single)",
                searchable=True,
                vector_search_dimensions=dimensions,
                vector_search_profile_name=VECTOR_PROFILE,
            ),
            SearchableField(name="title", type="Edm.String"),
            SearchableField(name="heading_path", type="Edm.String"),
            SimpleField(name="tenant_id", type="Edm.String", filterable=True),
            SimpleField(name="document_id", type="Edm.String", filterable=True),
            SimpleField(name="version_id", type="Edm.String", filterable=True),
            SimpleField(name="acl_groups", type="Collection(Edm.String)", filterable=True),
            SimpleField(name="doc_type", type="Edm.String", filterable=True),
            SimpleField(name="language", type="Edm.String", filterable=True),
            SimpleField(name="page", type="Edm.Int32", filterable=True, sortable=True),
            SimpleField(name="ordinal", type="Edm.Int32", sortable=True),
            SimpleField(name="content_sha256", type="Edm.String", filterable=True),
        ],
        vector_search=VectorSearch(
            algorithms=[HnswAlgorithmConfiguration(name="hnsw")],
            profiles=[VectorSearchProfile(name=VECTOR_PROFILE, algorithm_configuration_name="hnsw")],
        ),
        semantic_search=SemanticSearch(
            configurations=[
                SemanticConfiguration(
                    name=SEMANTIC_CONFIG,
                    prioritized_fields=SemanticPrioritizedFields(
                        title_field=SemanticField(field_name="title"),
                        content_fields=[SemanticField(field_name="content")],
                        keywords_fields=[SemanticField(field_name="heading_path")],
                    ),
                )
            ]
        ),
    )


def _odata_string(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def security_filter(tenant_id: UUID, acl_groups: Sequence[str]) -> str:
    """Tenant + ACL trimming on every query (AI-RAG-004)."""
    groups = ",".join(g.replace(",", "") for g in acl_groups) or "none"
    tenant = _odata_string(str(tenant_id))
    return f"tenant_id eq {tenant} and acl_groups/any(g: search.in(g, {_odata_string(groups)}, ','))"


class AzureSearchIndex:
    def __init__(self, settings: Settings) -> None:
        endpoint = settings.azure_search_endpoint
        credential: Any = (
            AzureKeyCredential(settings.azure_search_api_key.get_secret_value())
            if settings.azure_search_api_key is not None
            else DefaultAzureCredential()
        )
        self._name = settings.azure_search_index
        self._dimensions = settings.embed_dimensions
        self._indexes = SearchIndexClient(endpoint, credential)
        self._client = SearchClient(endpoint, self._name, credential)

    async def ensure_index(self) -> None:
        await self._indexes.create_or_update_index(index_definition(self._name, self._dimensions))

    async def upsert(self, chunks: Sequence[IndexedChunk]) -> None:
        documents = [
            {
                "id": c.id,
                "content": c.content,
                "content_en": c.content if c.language == "en" else "",
                "content_search": c.content_search,
                "content_vector": list(c.content_vector),
                "title": c.title,
                "heading_path": c.heading_path,
                "tenant_id": str(c.tenant_id),
                "document_id": str(c.document_id),
                "version_id": str(c.version_id),
                "acl_groups": list(c.acl_groups),
                "doc_type": c.doc_type,
                "language": c.language,
                "page": c.page,
                "ordinal": c.ordinal,
                "content_sha256": c.content_sha256,
            }
            for c in chunks
        ]
        for start in range(0, len(documents), 500):
            await self._client.merge_or_upload_documents(documents[start : start + 500])

    async def delete_document(self, tenant_id: UUID, document_id: UUID, *, keep_version: UUID | None = None) -> int:
        flt = f"tenant_id eq {_odata_string(str(tenant_id))} and document_id eq {_odata_string(str(document_id))}"
        if keep_version is not None:
            flt += f" and version_id ne {_odata_string(str(keep_version))}"
        results = await self._client.search(search_text="*", filter=flt, select=["id"], top=1000)
        ids = [{"id": r["id"]} async for r in results]
        if ids:
            await self._client.delete_documents(ids)
        return len(ids)

    async def version_info(self, tenant_id: UUID, document_id: UUID, version_id: UUID) -> tuple[str, int] | None:
        flt = (
            f"tenant_id eq {_odata_string(str(tenant_id))} and document_id eq {_odata_string(str(document_id))}"
            f" and version_id eq {_odata_string(str(version_id))}"
        )
        results = await self._client.search(
            search_text="*", filter=flt, select=["content_sha256"], top=1, include_total_count=True
        )
        count = int(await results.get_count() or 0)
        async for r in results:
            return str(r.get("content_sha256") or ""), count
        return None

    async def search(
        self,
        *,
        tenant_id: UUID,
        acl_groups: Sequence[str],
        query: str,
        query_search: str,
        vector: Sequence[float] | None,
        top: int,
    ) -> list[SearchHit]:
        vector_queries: list[VectorQuery] = (
            [VectorizedQuery(vector=list(vector), k_nearest_neighbors=50, fields="content_vector")] if vector else []
        )
        results = await self._client.search(
            search_text=f"{query} {query_search}".strip(),
            search_fields=["content", "content_en", "content_search", "title", "heading_path"],
            vector_queries=vector_queries,
            filter=security_filter(tenant_id, acl_groups),
            query_type="semantic",
            semantic_configuration_name=SEMANTIC_CONFIG,
            top=top,
        )
        hits: list[SearchHit] = []
        async for r in results:
            chunk = IndexedChunk(
                id=r["id"],
                tenant_id=UUID(r["tenant_id"]),
                document_id=UUID(r["document_id"]),
                version_id=UUID(r["version_id"]),
                title=r.get("title") or "",
                heading_path=r.get("heading_path") or "",
                page=int(r.get("page") or 0),
                language=r.get("language") or "ar",
                doc_type=r.get("doc_type") or "Other",
                acl_groups=list(r.get("acl_groups") or []),
                content=r.get("content") or "",
                content_search=r.get("content_search") or "",
                ordinal=int(r.get("ordinal") or 0),
                content_sha256=r.get("content_sha256") or "",
            )
            score = float(r.get("@search.reranker_score") or 0.0) / 4.0 or float(r.get("@search.score") or 0.0)
            hits.append(SearchHit(chunk, score))
        return hits

    async def aclose(self) -> None:
        await self._client.close()
        await self._indexes.close()
