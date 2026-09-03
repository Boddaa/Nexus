using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Pages;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.Pages.Services;

public class PageService : IPageService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PageService(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return false;
        }

        var userId = _currentUserService.UserId.Value;
        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                (w.OwnerId == userId || w.Members.Any(m => m.UserId == userId && !m.IsDeleted)), cancellationToken);
    }

    public async Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<PageTreeNodeDto>>(Error.Unauthorized);
        }

        // Single database query: fetch all active pages in the workspace
        var pages = await _context.Pages
            .AsNoTracking()
            .Where(p => p.WorkspaceId == workspaceId && !p.IsDeleted)
            .OrderBy(p => p.OrderIndex)
            .ThenBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // Build hierarchy in memory using lookup to avoid N+1 queries
        var lookup = pages.ToLookup(p => p.ParentPageId);

        List<PageTreeNodeDto> BuildSubTree(Guid? parentId)
        {
            return lookup[parentId]
                .OrderBy(p => p.OrderIndex)
                .ThenBy(p => p.CreatedAtUtc)
                .Select(p => new PageTreeNodeDto(
                    p.Id,
                    p.WorkspaceId,
                    p.ParentPageId,
                    p.Title,
                    p.Icon,
                    p.OrderIndex,
                    BuildSubTree(p.Id)))
                .ToList();
        }

        var rootNodes = BuildSubTree(null);
        return Result.Success<IReadOnlyList<PageTreeNodeDto>>(rootNodes);
    }

    public async Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<PageDto>(Error.Unauthorized);
        }

        var page = await _context.Pages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        if (page is null)
        {
            return Result.Failure<PageDto>(Error.NotFound);
        }

        var childPagesCount = await _context.Pages
            .CountAsync(p => p.ParentPageId == pageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        var notesCount = await _context.Notes
            .CountAsync(n => n.PageId == pageId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

        return Result.Success(new PageDto(
            page.Id,
            page.WorkspaceId,
            page.ParentPageId,
            page.Title,
            page.Icon,
            page.CoverImageUrl,
            page.ContentJson,
            page.OrderIndex,
            page.CreatedAtUtc,
            page.UpdatedAtUtc,
            childPagesCount,
            notesCount));
    }

    public async Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<PageDto>(Error.Unauthorized);
        }

        if (request.ParentPageId.HasValue)
        {
            var parentExists = await _context.Pages
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.ParentPageId.Value && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

            if (!parentExists)
            {
                return Result.Failure<PageDto>(new Error("Pages.InvalidParent", "The specified parent page does not exist in this workspace."));
            }
        }

        var page = new Page
        {
            WorkspaceId = workspaceId,
            ParentPageId = request.ParentPageId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "📄" : request.Icon.Trim(),
            CoverImageUrl = request.CoverImageUrl?.Trim(),
            ContentJson = string.IsNullOrWhiteSpace(request.ContentJson) ? "{}" : request.ContentJson.Trim(),
            OrderIndex = request.OrderIndex,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserId?.ToString()
        };

        _context.Pages.Add(page);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new PageDto(
            page.Id,
            page.WorkspaceId,
            page.ParentPageId,
            page.Title,
            page.Icon,
            page.CoverImageUrl,
            page.ContentJson,
            page.OrderIndex,
            page.CreatedAtUtc,
            page.UpdatedAtUtc,
            0,
            0));
    }

    public async Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<PageDto>(Error.Unauthorized);
        }

        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Id == pageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        if (page is null)
        {
            return Result.Failure<PageDto>(Error.NotFound);
        }

        page.Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim();
        page.Icon = string.IsNullOrWhiteSpace(request.Icon) ? "📄" : request.Icon.Trim();
        page.CoverImageUrl = request.CoverImageUrl?.Trim();
        page.ContentJson = request.ContentJson ?? "{}";
        page.OrderIndex = request.OrderIndex;
        page.UpdatedAtUtc = DateTime.UtcNow;
        page.UpdatedBy = _currentUserService.UserId?.ToString();

        await _context.SaveChangesAsync(cancellationToken);

        var childPagesCount = await _context.Pages
            .CountAsync(p => p.ParentPageId == pageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        var notesCount = await _context.Notes
            .CountAsync(n => n.PageId == pageId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

        return Result.Success(new PageDto(
            page.Id,
            page.WorkspaceId,
            page.ParentPageId,
            page.Title,
            page.Icon,
            page.CoverImageUrl,
            page.ContentJson,
            page.OrderIndex,
            page.CreatedAtUtc,
            page.UpdatedAtUtc,
            childPagesCount,
            notesCount));
    }

    public async Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<PageDto>(Error.Unauthorized);
        }

        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Id == pageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        if (page is null)
        {
            return Result.Failure<PageDto>(Error.NotFound);
        }

        // Prevent self-parenting
        if (request.TargetParentPageId.HasValue && request.TargetParentPageId.Value == pageId)
        {
            return Result.Failure<PageDto>(new Error("Pages.SelfParenting", "A page cannot be its own parent."));
        }

        // If target parent is specified, verify existence and check for cyclic dependencies
        if (request.TargetParentPageId.HasValue)
        {
            var targetParentId = request.TargetParentPageId.Value;

            var allPages = await _context.Pages
                .AsNoTracking()
                .Where(p => p.WorkspaceId == workspaceId && !p.IsDeleted)
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            if (!allPages.ContainsKey(targetParentId))
            {
                return Result.Failure<PageDto>(new Error("Pages.InvalidParent", "The target parent page does not exist in this workspace."));
            }

            // Cycle detection: traverse from targetParentId up to root to ensure moving page is not an ancestor
            var currentId = (Guid?)targetParentId;
            while (currentId.HasValue)
            {
                if (currentId.Value == pageId)
                {
                    return Result.Failure<PageDto>(new Error("Pages.CyclicDependency", "Cannot move a page under one of its descendants."));
                }

                if (allPages.TryGetValue(currentId.Value, out var ancestor))
                {
                    currentId = ancestor.ParentPageId;
                }
                else
                {
                    break;
                }
            }
        }

        page.ParentPageId = request.TargetParentPageId;
        page.OrderIndex = request.NewOrderIndex;
        page.UpdatedAtUtc = DateTime.UtcNow;
        page.UpdatedBy = _currentUserService.UserId?.ToString();

        await _context.SaveChangesAsync(cancellationToken);

        var childPagesCount = await _context.Pages
            .CountAsync(p => p.ParentPageId == pageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        var notesCount = await _context.Notes
            .CountAsync(n => n.PageId == pageId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

        return Result.Success(new PageDto(
            page.Id,
            page.WorkspaceId,
            page.ParentPageId,
            page.Title,
            page.Icon,
            page.CoverImageUrl,
            page.ContentJson,
            page.OrderIndex,
            page.CreatedAtUtc,
            page.UpdatedAtUtc,
            childPagesCount,
            notesCount));
    }

    public async Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(Error.Unauthorized);
        }

        var allPages = await _context.Pages
            .Where(p => p.WorkspaceId == workspaceId && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var targetPage = allPages.FirstOrDefault(p => p.Id == pageId);
        if (targetPage is null)
        {
            return Result.Failure(Error.NotFound);
        }

        // Recursively find all descendants to cascade soft-delete
        var lookup = allPages.ToLookup(p => p.ParentPageId);
        var pagesToSoftDelete = new List<Page>();

        void CollectSubTree(Page current)
        {
            pagesToSoftDelete.Add(current);
            foreach (var child in lookup[current.Id])
            {
                CollectSubTree(child);
            }
        }

        CollectSubTree(targetPage);

        var pageIdsToDelete = pagesToSoftDelete.Select(p => p.Id).ToList();
        var now = DateTime.UtcNow;
        var userIdStr = _currentUserService.UserId?.ToString();

        foreach (var p in pagesToSoftDelete)
        {
            p.IsDeleted = true;
            p.UpdatedAtUtc = now;
            p.UpdatedBy = userIdStr;
        }

        // Also cascade soft-delete to attached notes
        var attachedNotes = await _context.Notes
            .Where(n => n.WorkspaceId == workspaceId && n.PageId.HasValue && pageIdsToDelete.Contains(n.PageId.Value) && !n.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var note in attachedNotes)
        {
            note.IsDeleted = true;
            note.UpdatedAtUtc = now;
            note.UpdatedBy = userIdStr;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
