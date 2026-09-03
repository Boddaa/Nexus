namespace Nexus.Application.DTOs.Notes;

public record CreateNoteRequest(
    string Title,
    string Content,
    string ContentType = "markdown",
    bool IsPinned = false,
    Guid? PageId = null,
    List<string>? Tags = null);

public record UpdateNoteRequest(
    string Title,
    string Content,
    string ContentType = "markdown",
    bool IsPinned = false,
    Guid? PageId = null,
    List<string>? Tags = null);

public record NoteDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? PageId,
    string? PageTitle,
    string Title,
    string Content,
    string ContentType,
    bool IsPinned,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<string> Tags);

public record NoteSummaryDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? PageId,
    string? PageTitle,
    string Title,
    string ContentSnippet,
    string ContentType,
    bool IsPinned,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<string> Tags);
