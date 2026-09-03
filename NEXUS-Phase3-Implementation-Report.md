# NEXUS — Phase 3 Implementation Report

# Embeddings + Document Chunking + Vector Search + Hybrid Search

---

## 1. Implementation Summary

| Milestone | Scope & Description | Status |
|---|---|---|
| **3.1 Embedding Infrastructure** | Decoupled `IEmbeddingService`, `EmbeddingOptions`, `IEmbeddingProvider` with HTTP REST clients (`OpenAiEmbeddingProvider`, `OllamaEmbeddingProvider`). Strict zero fake/random vector policy. | **COMPLETE** ✅ |
| **3.2 Document Chunking** | `DocumentChunk` entity, `IDocumentChunker` and `DocumentChunker` with boundary-aware sliding window, `IDocumentChunkService` with transactional re-chunking reconciliation. | **COMPLETE** ✅ |
| **3.3 Embedding Generation** | `IChunkEmbeddingService` and `ChunkEmbeddingService` with bulk batching, strict dimension validation, concurrency guards (`Processing`), and failure tracking (`Failed`). | **COMPLETE** ✅ |
| **3.4 Vector Persistence & Similarity Search** | Lossless `varbinary(max)` vector storage (`VectorMath`), database-side workspace & soft-delete filtering, bounded candidate retrieval, and cosine similarity calculation. | **COMPLETE** ✅ |
| **3.4 Hybrid Search** | `SearchService` supporting `Keyword`, `Semantic`, and `Hybrid` modes. Dynamic score normalization, multi-signal candidate fusion with duplicate boosting, and deterministic ordering. | **COMPLETE** ✅ |
| **3.5 API Integration** | Extended `GET /api/workspaces/{workspaceId}/search` with `mode` and `topK`. Added `DocumentChunksController` for `/chunk`, `/embed`, and `/chunks`. | **COMPLETE** ✅ |
| **3.5 WPF Integration** | Modern Mode selector (`Keyword`, `Semantic`, `Hybrid`), search mode badges, seamless entity navigation to Pages, Notes, and parent Documents. | **COMPLETE** ✅ |
| **3.6 Tests & Migration** | Added 39 new tests across Domain, Application, Infrastructure, API, and Desktop. EF Core migration `AddDocumentChunksAndEmbeddings` scaffolded. Full test suite: **192 passed, 0 failed, 0 skipped**. | **COMPLETE** ✅ |

---

## 2. System Architecture

```text
                               ┌─────────────────────────┐
                               │   WPF Desktop Client    │
                               │ [Keyword|Semantic|Hybrid]
                               └────────────┬────────────┘
                                            │ HTTP REST
                                            ▼
                               ┌─────────────────────────┐
                               │     ASP.NET Core API    │
                               │ SearchController        │
                               │ DocumentChunksController│
                               └────────────┬────────────┘
                                            │
                      ┌─────────────────────┴─────────────────────┐
                      ▼                                           ▼
         ┌─────────────────────────┐                 ┌─────────────────────────┐
         │    Application Layer    │                 │   Infrastructure Layer  │
         │ ISearchService          │                 │ DocumentChunker         │
         │ IVectorSearchService    │                 │ EmbeddingService        │
         │ IDocumentChunkService   │                 │ OpenAiEmbeddingProvider │
         │ IChunkEmbeddingService  │                 │ OllamaEmbeddingProvider │
         │ IEmbeddingService       │                 │ AppDbContext            │
         └────────────┬────────────┘                 └────────────┬────────────┘
                      │                                           │
                      └─────────────────────┬─────────────────────┘
                                            ▼
                               ┌─────────────────────────┐
                               │       SQL Server        │
                               │ Workspaces, Documents   │
                               │ DocumentChunks (Binary) │
                               └─────────────────────────┘
```

### Embedding and Search Data Flow:
1. **Document Ingestion & Chunking**:
   - Extracted document text is processed by `IDocumentChunker` using natural paragraph/sentence boundaries and sliding window overlap.
   - Chunks are saved to `DocumentChunks` with `EmbeddingStatus = Pending`.
   - Existing obsolete chunks for the document are removed in the same transaction, preventing orphan or duplicate chunks.
2. **Embedding Generation**:
   - `IChunkEmbeddingService` retrieves pending chunks in batches (e.g. 25).
   - Generates vectors via `IEmbeddingService`.
   - Validates dimensions (`vector.Length == configuredDimensions`).
   - Encodes vectors to lossless IEEE 754 byte buffers (`varbinary(max)`) via `VectorMath.VectorToBytes`.
   - Marks status `Completed` (or `Failed` on provider error).
3. **Semantic & Hybrid Search**:
   - User initiates search in `Keyword`, `Semantic`, or `Hybrid` mode.
   - For `Semantic` / `Hybrid`, query vector is generated.
   - SQL Server applies database-side filters: `WorkspaceId == workspaceId && !IsDeleted && !Document.IsDeleted && EmbeddingStatus == Completed`.
   - A bounded candidate set is projected (avoiding loading the entire workspace into memory).
   - Cosine similarity is computed in C# over bounded candidate vectors.
   - For `Hybrid`, keyword scores and vector similarity scores are normalized to `[0, 100]`, weighted by `KeywordWeight` and `VectorWeight`, fused with a multi-signal match boost for items appearing in both channels, deduplicated, and paginated.

---

## 3. Provider Configuration

- **Abstraction**: `IEmbeddingService` in Application layer, implemented by `EmbeddingService` in Infrastructure.
- **Provider Implementations**:
  - `OpenAiEmbeddingProvider`: Talks to OpenAI-compatible `/v1/embeddings` endpoint via typed `HttpClient`.
  - `OllamaEmbeddingProvider`: Talks to local Ollama `/api/embeddings` endpoint.
- **Configuration Block** (`src/Nexus.API/appsettings.json`):
```json
"Embeddings": {
  "Provider": "None",
  "Model": "",
  "Dimensions": 1536,
  "ApiKey": "",
  "Endpoint": ""
}
```
- **Security**: No secrets committed. Default provider is `"None"`, which fails fast with descriptive `EmbeddingConfigurationException` if embedding generation is called without configuration. All unit and integration tests use isolated mock HTTP handlers without live network dependencies.

---

## 4. Chunking Strategy

- **Implementation**: `DocumentChunker` implementing `IDocumentChunker`.
- **Chunk Size & Overlap**:
  - `ChunkSize`: Configurable (defaults to 1000 characters).
  - `ChunkOverlap`: Configurable (defaults to 150 characters).
- **Boundary Awareness**:
  - Checks for paragraph breaks (`\n\n`), sentence punctuation (`. `, `! `, `? `, `; `), and word whitespace (` `) within lookback window.
  - Never cuts words in half arbitrarily.
  - Normalizes `\r\n` line endings and redundant whitespace.
- **Reprocessing & Idempotency**:
  - `DocumentChunkService.ChunkDocumentAsync` deletes existing chunks for the document before inserting newly generated chunks.
  - Generates deterministic `ChunkIndex` values (0, 1, 2, ...).
  - Deterministic boundaries guaranteed for identical text inputs.

---

## 5. Vector Storage & Performance

- **Persistence Representation**:
  Stored as `varbinary(max)` in the `DocumentChunks` table.
  Using `VectorMath.VectorToBytes(float[])` and `VectorMath.BytesToVector(byte[])` via `MemoryMarshal.Cast<byte, float>`.
  Zero loss in floating-point precision, ~60% smaller than JSON array representation, and ultra-fast zero-allocation span conversion.
- **Performance & Bounded Candidate Projection (Patch Rule 1 & Rule 13)**:
  - Database-side workspace filtering: `c.WorkspaceId == workspaceId`.
  - Database-side status & soft-delete filtering: `c.EmbeddingStatus == Completed && !c.IsDeleted && !c.Document.IsDeleted`.
  - Bounded candidate retrieval: Takes up to `Math.Clamp(topK * 10, 50, 300)` candidates.
  - Projection: Fetches only `Id, DocumentId, WorkspaceId, ChunkIndex, Text, PageNumber, EmbeddingVector, CreatedAtUtc, Document.Title`. Zero bytes of unneeded columns transferred.
  - `AsNoTracking()` applied to all read queries.
  - Similarity computed in application memory over this bounded candidate set using cosine similarity.
- **Indexes Added**:
  - `IX_DocumentChunks_WorkspaceId_DocumentId`
  - `IX_DocumentChunks_DocumentId_ChunkIndex`
  - `IX_DocumentChunks_WorkspaceId_EmbeddingStatus`

---

## 6. Search Capabilities & Ranking

### 6.1 Search Modes Supported
1. **`Keyword` (Default)**:
   - Preserves 100% of Phase 2.5 SQL/EF Core full-text keyword search across Pages, Notes, and Documents.
2. **`Semantic`**:
   - Embeds query, performs cosine similarity search over workspace chunks, and returns chunk-level matches attributed to parent documents.
3. **`Hybrid`**:
   - Executes both keyword search and vector chunk search.
   - **Score Normalization**:
     $\text{NormKeyword} = \frac{\text{Score}}{\text{MaxKeywordScore}} \times 100$
     $\text{NormVector} = \max(0, \text{CosineSimilarity}) \times 100$
   - **Weighted Fusion**:
     $\text{FinalScore} = (\text{NormKeyword} \times W_{\text{kw}}) + (\text{NormVector} \times W_{\text{vec}})$
     Default weights: $W_{\text{kw}} = 0.5$, $W_{\text{vec}} = 0.5$.
   - **Deduplication**:
     If a document matches both keyword and vector search, its score receives a multi-signal boost (+10) and snippet is enriched with the matching chunk text.
   - **Deterministic Sorting**: `Score DESC, CreatedAtUtc DESC`.
   - **Pagination**: Bounded `Take(pageSize)` on final ranked set.

---

## 7. API Endpoints

### 7.1 Changed Endpoints
- `GET /api/workspaces/{workspaceId}/search`:
  - Added query parameter `mode` (`Keyword`, `Semantic`, `Hybrid`). Default: `Keyword`.
  - Added query parameter `topK` (int, optional).

### 7.2 New Endpoints (`DocumentChunksController`)
- `POST /api/workspaces/{workspaceId}/documents/{documentId}/chunk`: Triggers document chunking with re-chunking reconciliation.
- `POST /api/workspaces/{workspaceId}/documents/{documentId}/embed`: Triggers embedding generation for un-embedded chunks.
- `GET /api/workspaces/{workspaceId}/documents/{documentId}/chunks`: Returns list of chunks for a document including chunk metadata and embedding status.

All endpoints are authenticated (`[Authorize]`), enforce workspace access, support `CancellationToken`, and adhere to standard error envelopes.

---

## 8. WPF Desktop Integration

- **Search View**:
  - Added a search mode selector ComboBox (`Keyword`, `Semantic`, `Hybrid`).
  - Added `SearchMode` badge on result item cards (displaying `Keyword`, `Semantic`, or `Hybrid` in subtle cyan).
- **Search ViewModel**:
  - Added `SelectedSearchMode` property and `SearchModeOptions` collection.
  - Included `mode` parameter in `ApiClient.SearchAsync`.
  - Maintained exact entity navigation: clicking any result opens the exact Page, Note, or parent Document in its respective view.

---

## 9. Test Suite Verification

### Summary:
- **Total Tests**: **192** (39 new tests added for Phase 3)
- **Status**: **100% Passed** (0 Failed, 0 Skipped across all 10 projects)

```text
Passed!  - Failed: 0, Passed:  4, Skipped: 0 - Nexus.Domain.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 36, Skipped: 0 - Nexus.Desktop.Tests.dll (net10.0-windows)
Passed!  - Failed: 0, Passed: 37, Skipped: 0 - Nexus.Infrastructure.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 89, Skipped: 0 - Nexus.Application.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 26, Skipped: 0 - Nexus.API.Tests.dll (net10.0)

Total: 192 Passed, 0 Failed, 0 Skipped
```

### Specific Test Coverage Added:
- `DocumentChunkerTests`: Short text, long text, empty/whitespace text, overlap, sentence boundary awareness, deterministic boundary stability, zero empty chunks.
- `VectorSimilarityTests`: Identical vectors (1.0), orthogonal (0.0), opposing (-1.0), zero vectors (0.0), dimension mismatch, binary roundtrip encoding.
- `DocumentChunkServiceTests`: Chunk creation, transactional re-chunking deletion, empty text validation, workspace access check.
- `ChunkEmbeddingServiceTests`: Batch embedding generation, idempotency skipping completed chunks, stale model/dimension re-generation, dimension mismatch validation, provider failure tracking.
- `VectorSearchServiceTests`: Top-K ordering, descending cosine similarity, strict workspace isolation, soft-deleted document/chunk exclusion.
- `HybridSearchTests`: Keyword-only, Semantic-only, Hybrid fusion with score normalization and deduplication, invalid mode rejection.
- `SearchEndpointsTests`: API integration for Keyword, Hybrid, and invalid mode rejection.
- `DocumentChunksEndpointsTests`: API integration for `/chunk`, `/embed`, and `/chunks`.
- `SearchViewModelTests`: Desktop ViewModel mode switching, search execution, mode options.

---

## 10. Build Status

```powershell
dotnet build Nexus.sln
# Build succeeded.
#   0 Warning(s)
#   0 Error(s)
```

---

## 11. Database Migrations

- **Migration Name**: `20260903095929_AddDocumentChunksAndEmbeddings`
- **Tables & Columns Added / Modified**:
  - `DocumentChunks.Text`: string not null.
  - `DocumentChunks.StartPosition`: int not null.
  - `DocumentChunks.EndPosition`: int not null.
  - `DocumentChunks.PageNumber`: int nullable.
  - `DocumentChunks.EmbeddingStatus`: int not null.
  - `DocumentChunks.EmbeddingVector`: `varbinary(max)` nullable.
  - `DocumentChunks.EmbeddingModel`: nvarchar(100) nullable.
  - `DocumentChunks.EmbeddingDimensions`: int nullable.
  - `DocumentChunks.WorkspaceId`: uniqueidentifier not null (FK to `Workspaces`, `ON DELETE NO ACTION`).
- **Indexes Created**:
  - `IX_DocumentChunks_WorkspaceId_DocumentId`
  - `IX_DocumentChunks_WorkspaceId_EmbeddingStatus`
  - `IX_DocumentChunks_DocumentId_ChunkIndex`

---

## 12. Architectural Transparency & Remaining Limitations

1. **Application-Side Vector Similarity**:
   In Phase 3 v1, vectors are stored as binary buffers (`varbinary(max)`) in SQL Server. Cosine similarity is computed in application memory over database-filtered, bounded candidate sets (up to 300 candidates). This provides strong performance and safety without requiring external vector databases or SQL Server 2025 native vector features.
2. **Chunk Scope**:
   Vector search currently indexes document chunks. Pages and Notes participate in keyword search and hybrid search via multi-source rank fusion, but do not yet generate embeddings (deferred to future phases).
3. **Embedding Provider Defaults**:
   The default configuration is `"Provider": "None"`. To enable live embedding generation, developers set `"Provider": "OpenAI"` (with an API key) or `"Provider": "Ollama"` (with local endpoint). When unconfigured, the application fails cleanly with descriptive error messages.

---

## 13. STOP Condition

All Phase 3 requirements are **complete, integrated, and verified**. Execution has halted; no Phase 4 features (RAG, LLM chat, AI answering) have been introduced.
