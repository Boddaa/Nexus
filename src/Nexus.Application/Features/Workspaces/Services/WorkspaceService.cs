using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Workspaces.Services;

public interface IWorkspaceService
{
    Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}

public class WorkspaceService : IWorkspaceService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public WorkspaceService(
        IAppDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;

        var ownedWorkspaces = await _context.Workspaces
            .AsNoTracking()
            .Where(w => w.OwnerId == userId && !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(w => new WorkspaceSummaryDto(w.Id, w.Name, w.Icon, w.ColorHex, WorkspaceRole.Owner))
            .ToListAsync(cancellationToken);

        var memberWorkspaces = await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.UserId == userId && !wm.IsDeleted && !wm.Workspace.IsDeleted)
            .OrderByDescending(wm => wm.CreatedAtUtc)
            .Select(wm => new WorkspaceSummaryDto(wm.Workspace.Id, wm.Workspace.Name, wm.Workspace.Icon, wm.Workspace.ColorHex, wm.Role))
            .ToListAsync(cancellationToken);

        var allWorkspaces = ownedWorkspaces.Concat(memberWorkspaces).ToList();
        return Result.Success<IReadOnlyList<WorkspaceSummaryDto>>(allWorkspaces);
    }

    public async Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<WorkspaceDto>(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .Include(w => w.Owner)
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == workspaceId && !w.IsDeleted, cancellationToken);

        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(Error.NotFound);
        }

        var hasAccess = workspace.OwnerId == userId || workspace.Members.Any(m => m.UserId == userId && !m.IsDeleted);
        if (!hasAccess)
        {
            return Result.Failure<WorkspaceDto>(Error.Unauthorized);
        }

        var pagesCount = await _context.Pages.CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);
        var notesCount = await _context.Notes.CountAsync(n => n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);
        var docsCount = await _context.Documents.CountAsync(d => d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);
        var tasksCount = await _context.Tasks.CountAsync(t => t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.Icon,
            workspace.ColorHex,
            workspace.OwnerId,
            workspace.Owner?.FullName ?? "Unknown",
            workspace.CreatedAtUtc,
            pagesCount,
            notesCount,
            docsCount,
            tasksCount));
    }

    public async Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<WorkspaceDto>(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user is null)
        {
            return Result.Failure<WorkspaceDto>(Error.Unauthorized);
        }

        var workspace = new Workspace
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "📁" : request.Icon,
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? "#3B82F6" : request.ColorHex,
            OwnerId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.Icon,
            workspace.ColorHex,
            workspace.OwnerId,
            user.FullName,
            workspace.CreatedAtUtc,
            0, 0, 0, 0));
    }

    public async Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<WorkspaceDto>(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;

        var workspace = await _context.Workspaces
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Id == workspaceId && !w.IsDeleted, cancellationToken);

        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(Error.NotFound);
        }

        if (workspace.OwnerId != userId)
        {
            return Result.Failure<WorkspaceDto>(Error.Unauthorized);
        }

        workspace.Name = request.Name.Trim();
        workspace.Description = request.Description?.Trim();
        workspace.Icon = request.Icon;
        workspace.ColorHex = request.ColorHex;
        workspace.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var pagesCount = await _context.Pages.CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);
        var notesCount = await _context.Notes.CountAsync(n => n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);
        var docsCount = await _context.Documents.CountAsync(d => d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);
        var tasksCount = await _context.Tasks.CountAsync(t => t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);

        return Result.Success(new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.Icon,
            workspace.ColorHex,
            workspace.OwnerId,
            workspace.Owner?.FullName ?? "Unknown",
            workspace.CreatedAtUtc,
            pagesCount,
            notesCount,
            docsCount,
            tasksCount));
    }

    public async Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId && !w.IsDeleted, cancellationToken);

        if (workspace is null)
        {
            return Result.Failure(Error.NotFound);
        }

        if (workspace.OwnerId != userId)
        {
            return Result.Failure(Error.Unauthorized);
        }

        workspace.IsDeleted = true;
        workspace.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
