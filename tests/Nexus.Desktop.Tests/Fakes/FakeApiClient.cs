using System.IO;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Services;
using Nexus.Domain.Common;

namespace Nexus.Desktop.Tests.Fakes;

public class FakeApiClient : IApiClient
{
    public List<PageTreeNodeDto> PageTrees { get; set; } = new();
    public List<PageDto> Pages { get; set; } = new();
    public List<NoteSummaryDto> NoteSummaries { get; set; } = new();
    public List<NoteDto> Notes { get; set; } = new();
    public List<WorkspaceSummaryDto> Workspaces { get; set; } = new();

    public bool ShouldFail { get; set; }
    public Error FailureError { get; set; } = new("Test.Error", "Simulated failure");

    public Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AuthResponse>(FailureError));
        return Task.FromResult(Result.Success(new AuthResponse(Guid.NewGuid(), request.Email, request.FullName, "User", "fake-token", DateTime.UtcNow.AddDays(1))));
    }

    public Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AuthResponse>(FailureError));
        return Task.FromResult(Result.Success(new AuthResponse(Guid.NewGuid(), request.Email, "Test User", "User", "fake-token", DateTime.UtcNow.AddDays(1))));
    }

    public Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<UserDto>(FailureError));
        return Task.FromResult(Result.Success(new UserDto(Guid.NewGuid(), "test@nexus.ai", "Test User", "User", null, DateTime.UtcNow)));
    }

    public Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(FailureError));
        return Task.FromResult(Result.Success<IReadOnlyList<WorkspaceSummaryDto>>(Workspaces));
    }

    public Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<WorkspaceDto>(FailureError));
        return Task.FromResult(Result.Success(new WorkspaceDto(workspaceId, "Test Workspace", "Desc", "📁", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)));
    }

    public Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<WorkspaceDto>(FailureError));
        return Task.FromResult(Result.Success(new WorkspaceDto(Guid.NewGuid(), request.Name, request.Description, request.Icon, request.ColorHex, Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)));
    }

    public Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<WorkspaceDto>(FailureError));
        return Task.FromResult(Result.Success(new WorkspaceDto(workspaceId, request.Name, request.Description, request.Icon, request.ColorHex, Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)));
    }

    public Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        return Task.FromResult(Result.Success());
    }

    // Pages
    public Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<PageTreeNodeDto>>(FailureError));
        return Task.FromResult(Result.Success<IReadOnlyList<PageTreeNodeDto>>(PageTrees));
    }

    public Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var page = Pages.FirstOrDefault(p => p.Id == pageId);
        if (page == null) return Task.FromResult(Result.Failure<PageDto>(new Error("Pages.NotFound", "Page not found.")));
        return Task.FromResult(Result.Success(page));
    }

    public Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var newPage = new PageDto(
            Guid.NewGuid(),
            workspaceId,
            request.ParentPageId,
            request.Title,
            request.Icon,
            request.CoverImageUrl,
            request.ContentJson,
            request.OrderIndex,
            DateTime.UtcNow,
            null,
            0,
            0);
        Pages.Add(newPage);
        PageTrees.Add(new PageTreeNodeDto(newPage.Id, newPage.WorkspaceId, newPage.ParentPageId, newPage.Title, newPage.Icon, newPage.OrderIndex, new List<PageTreeNodeDto>()));
        return Task.FromResult(Result.Success(newPage));
    }

    public Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var page = Pages.FirstOrDefault(p => p.Id == pageId);
        var updated = new PageDto(
            pageId,
            workspaceId,
            page?.ParentPageId,
            request.Title,
            request.Icon,
            request.CoverImageUrl,
            request.ContentJson,
            request.OrderIndex,
            DateTime.UtcNow,
            DateTime.UtcNow,
            page?.ChildPagesCount ?? 0,
            page?.NotesCount ?? 0);
        Pages.RemoveAll(p => p.Id == pageId);
        Pages.Add(updated);
        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var page = Pages.FirstOrDefault(p => p.Id == pageId);
        var moved = new PageDto(
            pageId,
            workspaceId,
            request.TargetParentPageId,
            page?.Title ?? "Page",
            page?.Icon ?? "📄",
            page?.CoverImageUrl,
            page?.ContentJson ?? "{}",
            request.NewOrderIndex,
            DateTime.UtcNow,
            DateTime.UtcNow,
            page?.ChildPagesCount ?? 0,
            page?.NotesCount ?? 0);
        Pages.RemoveAll(p => p.Id == pageId);
        Pages.Add(moved);
        return Task.FromResult(Result.Success(moved));
    }

    public Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        Pages.RemoveAll(p => p.Id == pageId);
        PageTrees.RemoveAll(p => p.Id == pageId);
        return Task.FromResult(Result.Success());
    }

    // Notes
    public Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<NoteSummaryDto>>(FailureError));
        var query = NoteSummaries.AsEnumerable();
        if (pageId.HasValue)
        {
            query = query.Where(n => n.PageId == pageId.Value);
        }
        if (isPinned.HasValue)
        {
            query = query.Where(n => n.IsPinned == isPinned.Value);
        }
        return Task.FromResult(Result.Success<IReadOnlyList<NoteSummaryDto>>(query.ToList()));
    }

    public Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var note = Notes.FirstOrDefault(n => n.Id == noteId);
        if (note == null) return Task.FromResult(Result.Failure<NoteDto>(new Error("Notes.NotFound", "Note not found.")));
        return Task.FromResult(Result.Success(note));
    }

    public Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var tags = request.Tags ?? new List<string>();
        var newNote = new NoteDto(
            Guid.NewGuid(),
            workspaceId,
            request.PageId,
            null,
            request.Title,
            request.Content,
            request.ContentType,
            request.IsPinned,
            DateTime.UtcNow,
            null,
            tags);
        Notes.Add(newNote);
        NoteSummaries.Add(new NoteSummaryDto(
            newNote.Id,
            newNote.WorkspaceId,
            newNote.PageId,
            null,
            newNote.Title,
            newNote.Content.Length > 50 ? newNote.Content[..50] : newNote.Content,
            newNote.ContentType,
            newNote.IsPinned,
            newNote.CreatedAtUtc,
            null,
            tags));
        return Task.FromResult(Result.Success(newNote));
    }

    public Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var tags = request.Tags ?? new List<string>();
        var updated = new NoteDto(
            noteId,
            workspaceId,
            request.PageId,
            null,
            request.Title,
            request.Content,
            request.ContentType,
            request.IsPinned,
            DateTime.UtcNow,
            DateTime.UtcNow,
            tags);
        Notes.RemoveAll(n => n.Id == noteId);
        Notes.Add(updated);

        NoteSummaries.RemoveAll(n => n.Id == noteId);
        NoteSummaries.Add(new NoteSummaryDto(
            noteId,
            workspaceId,
            request.PageId,
            null,
            request.Title,
            request.Content.Length > 50 ? request.Content[..50] : request.Content,
            request.ContentType,
            request.IsPinned,
            DateTime.UtcNow,
            DateTime.UtcNow,
            tags));

        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        Notes.RemoveAll(n => n.Id == noteId);
        NoteSummaries.RemoveAll(n => n.Id == noteId);
        return Task.FromResult(Result.Success());
    }

    // Documents
    public List<Nexus.Application.DTOs.Documents.DocumentSummaryDto> DocumentSummaries { get; set; } = new();
    public List<Nexus.Application.DTOs.Documents.DocumentDetailDto> DocumentDetails { get; set; } = new();

    public Task<Result<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentSummaryDto>>> GetDocumentsAsync(Guid workspaceId, Guid? pageId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentSummaryDto>>(FailureError));
        var query = DocumentSummaries.Where(d => d.WorkspaceId == workspaceId);
        if (pageId.HasValue) query = query.Where(d => d.PageId == pageId.Value);
        return Task.FromResult(Result.Success<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentSummaryDto>>(query.ToList()));
    }

    public Task<Result<Nexus.Application.DTOs.Documents.DocumentDetailDto>> GetDocumentByIdAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.DocumentDetailDto>(FailureError));
        var doc = DocumentDetails.FirstOrDefault(d => d.Id == documentId && d.WorkspaceId == workspaceId);
        if (doc == null) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.DocumentDetailDto>(Error.NotFound));
        return Task.FromResult(Result.Success(doc));
    }

    public Task<Result<Nexus.Application.DTOs.Documents.DocumentDto>> UploadDocumentAsync(
        Guid workspaceId,
        Stream fileStream,
        string fileName,
        string contentType,
        string? title = null,
        Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.DocumentDto>(FailureError));
        var id = Guid.NewGuid();
        var docDto = new Nexus.Application.DTOs.Documents.DocumentDto(
            id,
            workspaceId,
            pageId,
            null,
            title ?? fileName,
            fileName,
            contentType,
            Path.GetExtension(fileName).ToLowerInvariant(),
            fileStream.Length,
            Nexus.Domain.Enums.DocumentStatus.Processed,
            null,
            1,
            100,
            DateTime.UtcNow,
            null);

        DocumentSummaries.Add(new Nexus.Application.DTOs.Documents.DocumentSummaryDto(
            id,
            workspaceId,
            pageId,
            null,
            title ?? fileName,
            fileName,
            contentType,
            Path.GetExtension(fileName).ToLowerInvariant(),
            fileStream.Length,
            Nexus.Domain.Enums.DocumentStatus.Processed,
            DateTime.UtcNow));

        DocumentDetails.Add(new Nexus.Application.DTOs.Documents.DocumentDetailDto(
            id,
            workspaceId,
            pageId,
            null,
            title ?? fileName,
            fileName,
            contentType,
            Path.GetExtension(fileName).ToLowerInvariant(),
            fileStream.Length,
            Nexus.Domain.Enums.DocumentStatus.Processed,
            null,
            "Sample extracted text from document",
            1,
            34,
            DateTime.UtcNow,
            null));

        return Task.FromResult(Result.Success(docDto));
    }

    public Task<Result> DeleteDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        DocumentSummaries.RemoveAll(d => d.Id == documentId);
        DocumentDetails.RemoveAll(d => d.Id == documentId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<byte[]>> DownloadDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<byte[]>(FailureError));
        return Task.FromResult(Result.Success(new byte[] { 1, 2, 3, 4 }));
    }
}
