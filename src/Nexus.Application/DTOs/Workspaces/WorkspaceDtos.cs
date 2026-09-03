using Nexus.Domain.Enums;

namespace Nexus.Application.DTOs.Workspaces;

public record CreateWorkspaceRequest(
    string Name,
    string? Description,
    string Icon = "📁",
    string ColorHex = "#3B82F6");

public record UpdateWorkspaceRequest(
    string Name,
    string? Description,
    string Icon,
    string ColorHex);

public record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    string Icon,
    string ColorHex,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedAtUtc,
    int PagesCount,
    int NotesCount,
    int DocumentsCount,
    int TasksCount);

public record WorkspaceSummaryDto(
    Guid Id,
    string Name,
    string Icon,
    string ColorHex,
    WorkspaceRole CurrentUserRole);
