# NEXUS — Phase 2 — Task 2.5 Implementation Report

# Knowledge Search v1

---

## 1. Summary

| Attribute | Value |
|---|---|
| **Phase / Vertical Slice** | Phase 2 — Task 2.5: Knowledge Search v1 |
| **Frameworks** | .NET 10 (`net10.0`, `net10.0-windows`), ASP.NET Core, EF Core 10, WPF MVVM |
| **Status** | **COMPLETE & FULLY VERIFIED** |
| **Build Status** | `0 Warning(s)`, `0 Error(s)` |
| **Full Solution Test Suite** | **130 Tests Passed** (0 Failed, 0 Skipped across 10 projects) |
| **Database Migration** | Not required (uses existing schema and indexes) |
| **Search Paradigm** | Pure SQL Server / EF Core Keyword & Substring Search |
| **Searchable Sources** | Pages, Notes, Documents |
| **Security** | JWT authenticated, strict database-level workspace isolation, soft-delete exclusion |

Task 2.5 delivers the final vertical slice of Phase 2: a unified, authenticated, workspace-isolated full-text/keyword search engine for NEXUS. Users can query keywords and multi-word phrases across all pages, notes, and parsed document texts within their active workspace. Matches are scored via a deterministic relevance algorithm, enriched with contextual snippets, paginated, and rendered in a responsive WPF desktop interface with direct click-to-open navigation.

---

## 2. Search Architecture

The search flow follows Clean Architecture from WPF Desktop to SQL Server:

```text
                           NEXUS Search Architecture

 [WPF Desktop Client]
   ├── SearchViewModel (MVVM, CommunityToolkit, pagination, type filters, selection navigation)
   ├── SearchView (Dark enterprise layout, card results, score badges, snippets, pagination controls)
   └── ApiClient (HTTP client with Bearer JWT token & URL-encoded query parameters)
            │
            ▼  HTTP GET /api/workspaces/{workspaceId}/search?q=...&page=1&pageSize=20&type=...
 [Nexus.API]
   └── SearchController ([Authorize], ApiControllerBase, query validation)
            │
            ▼  Application Command Flow
 [Nexus.Application]
   ├── ISearchService / SearchService
   │     ├── Workspace Access Validation (Owner or Active Member check)
   │     ├── Input Validation (Empty, Max 200 chars, Page/PageSize clamping, Type whitelist)
   │     ├── Dynamic Predicate Builder (Translates multi-word tokens to SQL WHERE clauses)
   │     ├── Database-side Search Execution (EF Core AsNoTracking queries)
   │     ├── Deterministic Relevance Ranking (Title > Tags/Filename > Content)
   │     └── Safe Contextual Snippet Generator (Matches highlighted with surrounding window)
   └── DTOs (SearchRequest, SearchResultDto, PagedResult<T>)
            │
            ▼  Pure Relational EF Core / SQL Server Execution
 [Nexus.Infrastructure / Persistence]
   └── AppDbContext (Pages, Notes, Tags, Documents)
         - Enforces WorkspaceId == workspaceId
         - Enforces IsDeleted == false
         - Executes EF.Functions.Like queries on indexed columns
```

---

## 3. Searchable Sources

| Source | Searchable Fields | Specific Implementation Details |
|---|---|---|
| **Pages** | `Title`, `ContentJson` | Strips JSON structural symbols (`{}[]:,`) for clean text comparison and snippet generation. |
| **Notes** | `Title`, `Content`, `Tags` | Includes associated workspace tags via `Tags.Any(t => EF.Functions.Like(t.Name, pattern))`. |
| **Documents** | `Title`, `FileName`, `ExtractedText` | Searches parsed document plain text without exposing physical filesystem storage paths. |

---

## 4. Relevance Ranking (v1 Scoring Strategy)

Matches are ordered deterministically by `Score DESC, CreatedAtUtc DESC`. The scoring rules prioritize direct intent:

```text
1. Exact Title Match:              +150 points
2. Title Contains Full Query:      +100 points
3. Title Contains Token:           +40 points per token
4. Secondary (Tags/FileName) Full: +60 points
5. Secondary Contains Token:       +25 points per token
6. Content Contains Full Query:    +35 points
7. Content Contains Token:         +15 points per token
```

*Guarantees that documents/pages with relevant titles consistently rank higher than items with incidental content mentions.*

---

## 5. Snippet Generation

- **Length**: Maximum 250 characters.
- **Context Window**: Locates the first occurrence of the full search query (or highest-priority token) within the body.
- **Surrounding Padding**: Begins ~40 characters before the matched token.
- **Ellipses Formatting**: Adds leading `"... "` if the snippet starts mid-text, and trailing `"..."` if truncated before document end.
- **Fallback**: If match occurs in title/tags, returns leading body content.
- **Safety**: Safe against nulls, whitespaces, JSON markup, and boundary overflows.

---

## 6. Pagination & Query Limits

- **Pagination Model**: Reusable `PagedResult<T>` containing `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasPreviousPage`, and `HasNextPage`.
- **Query Length**: Maximum 200 characters (enforced with validation error `Search.QueryTooLong`).
- **Page Size Range**: Clamped between 1 and 50 (default 20).
- **Empty Queries**: Rejected with validation error `Search.EmptyQuery`.

---

## 7. Security

- [x] **JWT Authentication**: Secured with `[Authorize]` at the API controller level.
- [x] **Database-Level Workspace Isolation**: Users can only query workspaces where they are owner or active member. Every entity query applies `WorkspaceId == workspaceId` at the SQL level.
- [x] **Soft-Delete Exclusion**: Entities with `IsDeleted == true` are strictly filtered out in all entity queries.
- [x] **No Leaked Storage Paths**: Physical paths are completely excluded from search results.
- [x] **Limited Payload**: Huge raw documents are never loaded into memory; only snippets and metadata are projected.

---

## 8. Performance

- **`AsNoTracking()`**: Applied across all queries to bypass EF Core change tracking overhead.
- **Database-Side Filtering**: Filters run inside SQL Server using `EF.Functions.Like` with parameters.
- **Projection**: Queries project only required identifiers, titles, and snippets.
- **Limited Candidate Fetch**: Uses `Take(100)` per entity bucket before in-memory scoring to eliminate full-table loading risks.

---

## 9. Automated Test Suite Results

```text
Passed!  - Failed: 0, Passed:  4, Skipped: 0 - Nexus.Domain.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 12, Skipped: 0 - Nexus.Infrastructure.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 60, Skipped: 0 - Nexus.Application.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 20, Skipped: 0 - Nexus.API.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 34, Skipped: 0 - Nexus.Desktop.Tests.dll (net10.0-windows)

Total: 130 Passed, 0 Failed, 0 Skipped (100% Pass Rate across all 10 projects)
```

### Breakdown of Tests Added in Task 2.5:
- **`Nexus.Application.Tests/SearchServiceTests.cs`** (14 tests):
  - `SearchAsync_Should_Find_Matching_Pages_Notes_And_Documents`
  - `SearchAsync_Should_Be_Case_Insensitive`
  - `SearchAsync_Stronger_Title_Match_Should_Rank_Higher_Than_Content_Match`
  - `SearchAsync_Snippet_Should_Contain_Context_Around_Match_And_Not_Full_Text`
  - `SearchAsync_Type_Filter_Should_Only_Return_Specified_Entity_Type`
  - `SearchAsync_Empty_Query_Should_Fail_Validation`
  - `SearchAsync_Query_Over_200_Chars_Should_Fail_Validation`
  - `SearchAsync_Invalid_Type_Filter_Should_Fail_Validation`
  - `SearchAsync_Workspace_Isolation_Should_Never_Return_Another_Workspace_Results`
  - `SearchAsync_Unauthorized_User_Cannot_Search_Foreign_Workspace`
  - `SearchAsync_Soft_Deleted_Entities_Should_Not_Be_Returned`
  - `SearchAsync_Pagination_Should_Work_Correctly`
- **`Nexus.API.Tests/SearchEndpointsTests.cs`** (6 tests):
  - `Search_Without_Authentication_Should_Return_401`
  - `Search_Empty_Query_Should_Return_400`
  - `Search_Invalid_Type_Should_Return_400`
  - `Search_Should_Return_Unified_Matches_Across_Pages_Notes_And_Documents`
  - `Search_With_Type_Filter_Should_Only_Return_Specified_Type`
  - `Search_Workspace_Isolation_Should_Not_Return_Foreign_Workspace_Items`
- **`Nexus.Desktop.Tests/SearchViewModelTests.cs`** (8 tests):
  - `SearchAsync_Should_Populate_Results_And_Update_States`
  - `SearchAsync_Empty_Query_Should_Set_ErrorMessage`
  - `SearchAsync_No_Workspace_Should_Set_ErrorMessage`
  - `SetTypeFilterAsync_Should_Update_Filter_And_Requery`
  - `OpenResult_Page_Should_Navigate_To_PagesViewModel`
  - `OpenResult_Note_Should_Navigate_To_NotesViewModel`
  - `OpenResult_Document_Should_Navigate_To_DocumentsViewModel`
  - `SearchAsync_Api_Failure_Should_Set_ErrorMessage_And_Reset_Busy`

---

## 10. Known Limitations (Strict Scope Preservation)

- **No Embeddings / Vector Search**: Semantic similarity queries are deferred to Phase 3.
- **No AI / LLM Ranking**: Uses pure deterministic relational scoring.
- **No SQL Server Full-Text Search Catalog**: Utilizes standard relational SQL matching to maintain complete portability across development and testing environments.
- **No OCR**: Only extracted text from supported documents is indexed.

---

## 11. Final Status

- **Task 2.5 Status**: **COMPLETE & FULLY VERIFIED.**
- **Phase 2 Status**: **ALL 5 VERTICAL SLICES COMPLETE.**
  - Task 2.1 — Pages & Notes Application Layer ✅
  - Task 2.2 — Pages & Notes Web API Layer ✅
  - Task 2.3 — Pages & Notes WPF Desktop Integration ✅
  - Task 2.4 — Document Storage & Parsing ✅
  - Task 2.5 — Knowledge Search v1 ✅
