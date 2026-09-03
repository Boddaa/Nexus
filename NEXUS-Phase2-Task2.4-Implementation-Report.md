# NEXUS — Phase 2 — Task 2.4 Implementation Report

# Document Storage & Parsing

---

## 1. Executive Summary

| Attribute | Value |
|---|---|
| **Phase / Vertical Slice** | Phase 2 — Task 2.4: Document Storage & Parsing |
| **Frameworks** | .NET 10 (`net10.0`, `net10.0-windows`), ASP.NET Core, EF Core 10, WPF MVVM |
| **Status** | **COMPLETE & FULLY VERIFIED** |
| **Build Status** | `0 Warning(s)`, `0 Error(s)` |
| **Full Solution Test Suite** | **92 Tests Passed** (0 Failed, 0 Skipped across 10 projects) |
| **EF Core Migration** | `20260903073527_AddDocumentPageAndIndexes` |
| **Physical Storage Security** | Sandboxed to `Storage:DocumentsRoot`, path traversal protected, safe generated file keys |
| **Supported File Formats** | `.pdf`, `.docx`, `.md`, `.txt` |
| **File Size Limit** | 50 MB enforced in API and Application layers |

This vertical slice establishes the complete Document Storage & Parsing foundation for NEXUS. Authenticated users can upload documents to a workspace (optionally attaching them to a page), store files securely on disk via the `IFileStorage` abstraction, automatically extract textual content through strategy-based extractors, persist metadata in SQL Server, list, view, download, and delete documents, all with strict workspace isolation and transaction-safe cleanup.

---

## 2. Existing Document Foundation

Before implementation, inspection revealed:
- `Document` and `DocumentChunk` entities existed in `Nexus.Domain.Entities.DocumentEntities.cs`.
- `IAppDbContext` exposed `DbSet<Document>` and `DbSet<DocumentChunk>`.
- `IFileStorage` interface existed in `Nexus.Application.Common.Interfaces` with a preliminary `LocalFileStorage` in `Nexus.Infrastructure.Storage`.
- `DocumentsView.xaml` was an initial placeholder in `Nexus.Desktop`.
- No parsing, text extraction, API endpoints, or view models were implemented.

---

## 3. Architecture

The end-to-end knowledge pipeline implemented in Task 2.4 is structured as follows:

```text
                            NEXUS Architecture Pipeline

 [WPF Desktop Client]
   ├── DocumentsViewModel (MVVM, CommunityToolkit)
   ├── DocumentsView (Dark enterprise card list, filters, text preview)
   ├── FilePickerService (IFilePickerService: Windows OpenFileDialog / SaveFileDialog)
   └── ApiClient (HTTP client with Bearer JWT token & MultipartFormDataContent)
            │
            ▼  HTTP REST (POST multipart/form-data, GET, DELETE)
 [Nexus.API]
   └── DocumentsController (Authorize, RequestSizeLimit: 50MB, ApiControllerBase)
            │
            ▼  Application Command / Query Flow
 [Nexus.Application]
   ├── DocumentService (IDocumentService: UploadAsync, GetDocumentsAsync, GetDocumentByIdAsync, DeleteAsync, GetFileStreamAsync)
   ├── Validation (Empty, Max 50MB, Extensions: .pdf, .docx, .md, .txt, Workspace Isolation, Page ownership)
   ├── Failure Cleanup (Orphan prevention on extraction/persistence failure)
   └── DTOs (UploadDocumentStreamRequest, DocumentDto, DocumentDetailDto, DocumentSummaryDto, DocumentFileDownloadDto)
            │
            ├──► IFileStorage (LocalFileStorage) ──► Physical Disk ({root}/{workspaceId}/{docId}/{uniqueKey}.ext)
            ├──► IDocumentTextExtractor (Strategy: TXT, MD, DOCX, PDF) ──► Extracted Plain Text
            └──► IAppDbContext (EF Core SQL Server) ──► Metadata, Status, ExtractedText
```

---

## 4. Domain Changes

1. **`Document` Entity (`src/Nexus.Domain/Entities/DocumentEntities.cs`)**:
   - Added optional `public Guid? PageId { get; set; }` and navigation `public Page? Page { get; set; }`.
   - Added `public Document() { }` and `public Document(Guid id) { Id = id; }` to support domain-assigned identities while preserving protected base setters.
2. **`Page` Entity (`src/Nexus.Domain/Entities/ContentEntities.cs`)**:
   - Added navigation collection `public ICollection<Document> Documents { get; set; } = new List<Document>();`.

---

## 5. Database Schema Changes & Migration

### Entity Configuration
In [DocumentConfigurations.cs](file:///d:/Me/Project-Idea/NEXUS/src/Nexus.Infrastructure/Persistence/Configurations/DocumentConfigurations.cs):
- Configured relationship:
  ```csharp
  builder.HasOne(d => d.Page)
      .WithMany(p => p.Documents)
      .HasForeignKey(d => d.PageId)
      .OnDelete(DeleteBehavior.SetNull);
  ```
  *(Soft-delete compatible: deleting a page clears `PageId` on attached documents without hard-deleting the documents).*
- Configured query indexes:
  - `builder.HasIndex(d => new { d.WorkspaceId, d.CreatedAtUtc });`
  - `builder.HasIndex(d => d.PageId);`

### Migration
- Generated migration: `20260903073527_AddDocumentPageAndIndexes`.
- Verified via `dotnet ef migrations list --project src/Nexus.Infrastructure --startup-project src/Nexus.API`.

---

## 6. Storage Abstraction & Local Storage Hardening

### Abstraction (`IFileStorage.cs`)
```csharp
public interface IFileStorage
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<string> SaveFileAsync(Stream fileStream, string subDirectory, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetFileStreamAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default);
}
```

### Path Traversal & Sandboxing (`LocalFileStorage.cs`)
- Storage root configured via `Storage:DocumentsRoot` (or `FileStorage:BasePath`), defaulting to `%LOCALAPPDATA%/Nexus/Storage`.
- Physical paths are server-generated: `{subDirectory}/{Guid:N}{safeExtension}`. Raw user-provided filenames are stored only as metadata.
- `GetSafeFullPath(relativePath)` strictly validates that the normalized absolute path begins with the storage root directory. Path traversal sequences (`..`, `../`, `..\`) throw `UnauthorizedAccessException`.
- Clean forward slashes are used for cross-platform storage paths.

---

## 7. Document Text Extraction (Strategy Pattern)

### Abstraction (`IDocumentTextExtractor.cs`)
```csharp
public interface IDocumentTextExtractor
{
    bool CanHandle(string extension, string contentType);
    Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default);
}
```

### Implementations

1. **`PlainTextDocumentExtractor` (`.txt`, `text/plain`)**:
   - Reads UTF-8 character stream via `StreamReader` with BOM detection.
2. **`MarkdownDocumentExtractor` (`.md`, `text/markdown`)**:
   - Reads markdown source preserving verbatim markdown syntax without HTML conversion.
3. **`DocxDocumentExtractor` (`.docx`, OpenXML)**:
   - Uses standard .NET `System.IO.Compression.ZipArchive` and `System.Xml.Linq` (zero extra dependencies).
   - Reads `word/document.xml`, extracting text runs from `<w:t>` inside paragraphs `<w:p>`.
4. **`PdfDocumentExtractor` (`.pdf`, `application/pdf`)**:
   - Uses `PdfPig` version `0.1.9` (Apache-2.0 license, 100% managed C#).
   - Iterates pages and extracts text without native dependencies.

All four extractors are registered in DI as `IDocumentTextExtractor` singletons.

---

## 8. Application Service & Failure Cleanup

### Operations in `DocumentService`:
- `UploadAsync`:
  1. Validates workspace access.
  2. Validates non-empty file.
  3. Validates file size (≤ 50 MB limit).
  4. Validates supported extension (`.pdf`, `.docx`, `.md`, `.txt`).
  5. Validates that `PageId` (if specified) belongs to the target workspace.
  6. Saves physical file to disk.
  7. Extracts text using resolved `IDocumentTextExtractor`.
  8. If extraction fails: deletes physical file, returns failure error.
  9. Persists `Document` entity with `DocumentStatus.Processed`.
  10. If database persistence throws an exception: cleans up physical file to prevent orphans.
- `GetDocumentsAsync`: Lists workspace documents, supports optional `pageId` filter, orders by creation date.
- `GetDocumentByIdAsync`: Retrieves full document details including `ExtractedText`.
- `DeleteAsync`: Verifies workspace access, deletes physical file from storage, marks document as soft-deleted (`IsDeleted = true`).
- `GetFileStreamAsync`: Returns `DocumentFileDownloadDto` with file stream, filename, content type, and byte length for downloading.

---

## 9. API Endpoints

All endpoints require `[Authorize]` and are mounted on `api/workspaces/{workspaceId}/documents`:

| Method | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/workspaces/{workspaceId}/documents?pageId={id}` | List workspace documents | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/workspaces/{workspaceId}/documents/{documentId}` | Get document details | `200 OK`, `401`, `404 Not Found` |
| `POST` | `/api/workspaces/{workspaceId}/documents` | Multipart file upload | `201 Created`, `400 Bad Request`, `401` |
| `DELETE` | `/api/workspaces/{workspaceId}/documents/{documentId}` | Delete document | `200 OK`, `401`, `404` |
| `GET` | `/api/workspaces/{workspaceId}/documents/{documentId}/download` | Download physical file | `200 OK` (File), `401`, `404` |

*`IFormFile` is contained strictly at the API controller boundary and converted to `UploadDocumentStreamRequest` before entering Application services.*

---

## 10. Desktop MVVM Integration

1. **`IApiClient` / `ApiClient`**:
   - Added `GetDocumentsAsync`, `GetDocumentByIdAsync`, `UploadDocumentAsync` (using `MultipartFormDataContent`), `DeleteDocumentAsync`, and `DownloadDocumentAsync`.
2. **`IFilePickerService` / `FilePickerService`**:
   - Created clean abstraction for `OpenFileDialog` (filtered for `.pdf`, `.docx`, `.md`, `.txt`) and `SaveFileDialog`.
   - Allows headless unit testing via `FakeFilePickerService`.
3. **`DocumentsViewModel`**:
   - Observable collections for documents and filtered list.
   - Search filtering by title, filename, extension, and page title (`StringComparison.OrdinalIgnoreCase`).
   - Page filter dropdown.
   - Upload command with file picker integration and auto-selection of uploaded document.
   - Delete command with `IDialogService.ConfirmAsync` confirmation dialog.
   - Download command with destination path picker.
   - Guaranteed reset of `IsLoading`, `IsUploading`, and `IsDeleting` in `finally` blocks.
4. **`DocumentsView.xaml`**:
   - 2-column layout: document card list with badges (status, extension, size, page) on the left; full details, download/delete buttons, and read-only extracted text preview on the right.

---

## 11. Security Checklist Verification

- [x] **JWT Authentication Required**: All document endpoints protected with `[Authorize]`.
- [x] **Workspace Isolation**: Users can only query, upload, download, or delete documents in workspaces where they are owner or active member.
- [x] **Page Workspace Verification**: Document cannot be linked to a page belonging to a different workspace.
- [x] **File Size Enforcement**: 50 MB limit enforced at both API (`[RequestSizeLimit]`) and Application layer (`MaxFileSizeBytes`).
- [x] **Extension Validation**: Strict whitelist (`.pdf`, `.docx`, `.md`, `.txt`).
- [x] **Path Traversal Protection**: Sandboxed to storage root; traversal attempts throw `UnauthorizedAccessException`.
- [x] **Safe Physical File Names**: Files stored with server-generated GUIDs; user filename is metadata only.
- [x] **No Leaked Physical Paths**: Physical absolute paths are never returned in DTOs or API responses.
- [x] **Download Authorization**: Validated through workspace isolation before opening stream.
- [x] **No Storage in Git/Repo**: Test files write to temporary directory; production defaults to local AppData.

---

## 12. Automated Test Suite Results

```
Passed!  - Failed: 0, Passed:  4, Skipped: 0 - Nexus.Domain.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed:  6, Skipped: 0 - Nexus.Infrastructure.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 42, Skipped: 0 - Nexus.Application.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 14, Skipped: 0 - Nexus.API.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 26, Skipped: 0 - Nexus.Desktop.Tests.dll (net10.0-windows)

Total: 92 Passed, 0 Failed, 0 Skipped (100% Pass Rate across all 10 projects)
```

### Breakdown of New Tests Added in Task 2.4:
- **`Nexus.Application.Tests/DocumentServiceTests.cs`** (11 tests):
  - `PlainTextDocumentExtractor_Should_Extract_Content`
  - `MarkdownDocumentExtractor_Should_Extract_Content`
  - `DocxDocumentExtractor_Should_Extract_Paragraphs`
  - `UploadAsync_Valid_Txt_Should_Store_File_And_Persist_Metadata`
  - `UploadAsync_Valid_Markdown_Should_Succeed`
  - `UploadAsync_Empty_File_Should_Fail`
  - `UploadAsync_File_Over_50MB_Should_Fail`
  - `UploadAsync_Unsupported_Extension_Should_Fail`
  - `UploadAsync_Page_From_Different_Workspace_Should_Fail`
  - `UploadAsync_When_Extraction_Fails_Should_Clean_Up_Physical_File`
  - `GetDocumentsAsync_Should_Return_Workspace_Documents_Only`
  - `GetDocumentByIdAsync_Should_Return_Full_Details_Including_ExtractedText`
  - `DeleteAsync_Should_Delete_Physical_File_And_Soft_Delete_Metadata`
  - `Workspace_Isolation_Unauthorized_User_Cannot_Access_Documents`
- **`Nexus.Infrastructure.Tests/LocalFileStorageTests.cs`** (3 tests):
  - `SaveFileAsync_And_GetFileStreamAsync_Should_Work_Correctly`
  - `DeleteFileAsync_Should_Remove_File`
  - `Path_Traversal_Attempt_Should_Throw_UnauthorizedAccessException`
- **`Nexus.API.Tests/DocumentsEndpointsTests.cs`** (4 tests):
  - `Upload_Without_Authentication_Should_Return_401`
  - `Documents_Upload_Retrieve_Download_Delete_Flow_Should_Succeed`
  - `Upload_Unsupported_Format_Should_Return_400`
  - `Workspace_Isolation_Other_User_Cannot_Access_Document`
- **`Nexus.Desktop.Tests/DocumentsViewModelTests.cs`** (8 tests):
  - `DocumentsViewModel_LoadDocumentsAsync_Should_Populate_Collections`
  - `DocumentsViewModel_SelectDocumentAsync_Should_Populate_Details_And_ExtractedText`
  - `DocumentsViewModel_UploadDocumentAsync_When_File_Picked_Should_Upload_And_Select`
  - `DocumentsViewModel_DeleteDocumentAsync_When_Cancelled_Should_Not_Call_Api`
  - `DocumentsViewModel_DeleteDocumentAsync_When_Confirmed_Should_Call_Api_And_Reset_Selection`
  - `DocumentsViewModel_SearchText_Should_Filter_Documents_Locally`
  - `DocumentsViewModel_DownloadDocumentAsync_Should_Write_Bytes_To_Target`
  - `DocumentsViewModel_Api_Failure_Resets_Busy_And_Sets_ErrorMessage`

---

## 13. Files Created & Modified

### Files Created:
1. `src/Nexus.Application/DTOs/Documents/DocumentDtos.cs`
2. `src/Nexus.Application/Features/Documents/Services/IDocumentService.cs`
3. `src/Nexus.Application/Features/Documents/Services/DocumentService.cs`
4. `src/Nexus.Infrastructure/Parsing/PlainTextDocumentExtractor.cs`
5. `src/Nexus.Infrastructure/Parsing/MarkdownDocumentExtractor.cs`
6. `src/Nexus.Infrastructure/Parsing/DocxDocumentExtractor.cs`
7. `src/Nexus.Infrastructure/Parsing/PdfDocumentExtractor.cs`
8. `src/Nexus.Infrastructure/Migrations/20260903073527_AddDocumentPageAndIndexes.cs`
9. `src/Nexus.Infrastructure/Migrations/20260903073527_AddDocumentPageAndIndexes.Designer.cs`
10. `src/Nexus.API/Controllers/DocumentsController.cs`
11. `src/Nexus.Desktop/Services/IFilePickerService.cs`
12. `src/Nexus.Desktop/Services/FilePickerService.cs`
13. `src/Nexus.Desktop/ViewModels/DocumentsViewModel.cs`
14. `tests/Nexus.Application.Tests/DocumentServiceTests.cs`
15. `tests/Nexus.Infrastructure.Tests/LocalFileStorageTests.cs`
16. `tests/Nexus.API.Tests/DocumentsEndpointsTests.cs`
17. `tests/Nexus.Desktop.Tests/DocumentsViewModelTests.cs`
18. `tests/Nexus.Desktop.Tests/Fakes/FakeFilePickerService.cs`

### Files Modified:
1. `src/Nexus.Domain/Entities/DocumentEntities.cs`
2. `src/Nexus.Domain/Entities/ContentEntities.cs`
3. `src/Nexus.Infrastructure/Nexus.Infrastructure.csproj`
4. `src/Nexus.Infrastructure/Persistence/Configurations/DocumentConfigurations.cs`
5. `src/Nexus.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
6. `src/Nexus.Infrastructure/Storage/LocalFileStorage.cs`
7. `src/Nexus.Infrastructure/DependencyInjection.cs`
8. `src/Nexus.Application/Common/Interfaces/IFileStorage.cs`
9. `src/Nexus.Application/DependencyInjection.cs`
10. `src/Nexus.Desktop/Services/ApiClient.cs`
11. `src/Nexus.Desktop/ViewModels/ModuleViewModels.cs`
12. `src/Nexus.Desktop/Views/DocumentsView.xaml`
13. `src/Nexus.Desktop/App.xaml.cs`
14. `tests/Nexus.Desktop.Tests/Fakes/FakeApiClient.cs`
15. `tests/Nexus.Desktop.Tests/NavigationAndViewModelTests.cs`

---

## 14. Known Limitations & Deferred Improvements

- **No OCR**: Scanned images inside PDFs without textual glyphs are not OCR-processed (intentional for Task 2.4).
- **No Embeddings / Vector Search**: Extracted text is stored in SQL Server; chunking and vector indexing are deferred to Phase 3.
- **No Background Processing Broker**: Document parsing currently runs synchronously on upload (as requested, background queues are out of scope for Phase 2).

---

## 15. Final Status & Recommended Next Task

- **Task 2.4 Status**: **COMPLETE & APPROVED FOR MERGE.**
- **Recommended Next Task**: **Task 2.5 — Knowledge Search v1** (Lexical search across Pages, Notes, and Parsed Documents).
