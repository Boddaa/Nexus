namespace Nexus.Application.DTOs.Pages;

public record CreatePageRequest(
    string Title,
    string Icon = "📄",
    string? CoverImageUrl = null,
    string ContentJson = "{}",
    Guid? ParentPageId = null,
    int OrderIndex = 0);

public record UpdatePageRequest(
    string Title,
    string Icon = "📄",
    string? CoverImageUrl = null,
    string ContentJson = "{}",
    int OrderIndex = 0);

public record MovePageRequest(
    Guid? TargetParentPageId,
    int NewOrderIndex = 0);

public record PageDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? ParentPageId,
    string Title,
    string Icon,
    string? CoverImageUrl,
    string ContentJson,
    int OrderIndex,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int ChildPagesCount,
    int NotesCount);

public record PageTreeNodeDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? ParentPageId,
    string Title,
    string Icon,
    int OrderIndex,
    IReadOnlyList<PageTreeNodeDto> Children);

public record PageSummaryDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? ParentPageId,
    string Title,
    string Icon,
    int OrderIndex);
