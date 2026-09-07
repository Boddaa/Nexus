using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Notes;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.Notes.Services;

public class NoteService : INoteService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public NoteService(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private async Task<Result> ValidateWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;
        var hasAccess = await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                (w.OwnerId == userId || w.Members.Any(m => m.UserId == userId && !m.IsDeleted)), cancellationToken);

        if (!hasAccess)
        {
            return Result.Failure(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(
        Guid workspaceId,
        Guid? pageId = null,
        bool? isPinned = null,
        CancellationToken cancellationToken = default)
    {
        var accessCheck = await ValidateWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<NoteSummaryDto>>(accessCheck.Error);
        }

        var query = _context.Notes
            .AsNoTracking()
            .Where(n => n.WorkspaceId == workspaceId && !n.IsDeleted);

        if (pageId.HasValue)
        {
            query = query.Where(n => n.PageId == pageId.Value);
        }

        if (isPinned.HasValue)
        {
            query = query.Where(n => n.IsPinned == isPinned.Value);
        }

        var notes = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAtUtc ?? n.CreatedAtUtc)
            .Select(n => new NoteSummaryDto(
                n.Id,
                n.WorkspaceId,
                n.PageId,
                n.Page != null ? n.Page.Title : null,
                n.Title,
                n.Content.Length > 150 ? n.Content.Substring(0, 150) + "..." : n.Content,
                n.ContentType,
                n.IsPinned,
                n.CreatedAtUtc,
                n.UpdatedAtUtc,
                n.Tags.Where(t => !t.IsDeleted).Select(t => t.Name).ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NoteSummaryDto>>(notes);
    }

    public async Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        var accessCheck = await ValidateWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result.Failure<NoteDto>(accessCheck.Error);
        }

        var note = await _context.Notes
            .AsNoTracking()
            .Include(n => n.Page)
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == noteId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

        if (note is null)
        {
            return Result.Failure<NoteDto>(Error.NotFound);
        }

        return Result.Success(new NoteDto(
            note.Id,
            note.WorkspaceId,
            note.PageId,
            note.Page?.Title,
            note.Title,
            note.Content,
            note.ContentType,
            note.IsPinned,
            note.CreatedAtUtc,
            note.UpdatedAtUtc,
            note.Tags.Where(t => !t.IsDeleted).Select(t => t.Name).ToList()));
    }

    public async Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var accessCheck = await ValidateWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result.Failure<NoteDto>(accessCheck.Error);
        }

        string? pageTitle = null;
        if (request.PageId.HasValue)
        {
            var page = await _context.Pages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PageId.Value && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

            if (page is null)
            {
                return Result.Failure<NoteDto>(new Error("Notes.InvalidPage", "The specified page does not exist in this workspace."));
            }

            pageTitle = page.Title;
        }

        var note = new Note
        {
            WorkspaceId = workspaceId,
            PageId = request.PageId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled Note" : request.Title.Trim(),
            Content = request.Content ?? string.Empty,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "markdown" : request.ContentType.Trim().ToLowerInvariant(),
            IsPinned = request.IsPinned,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserId?.ToString()
        };

        // Handle tags if specified
        var tagNames = new List<string>();
        if (request.Tags != null && request.Tags.Count > 0)
        {
            var cleanTags = request.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList();
            var existingTags = await _context.Tags
                .Where(t => t.WorkspaceId == workspaceId && cleanTags.Contains(t.Name) && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var tag in existingTags)
            {
                note.Tags.Add(tag);
                tagNames.Add(tag.Name);
            }

            foreach (var tagName in cleanTags.Except(existingTags.Select(t => t.Name)))
            {
                var newTag = new Tag
                {
                    WorkspaceId = workspaceId,
                    Name = tagName,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = _currentUserService.UserId?.ToString()
                };
                _context.Tags.Add(newTag);
                note.Tags.Add(newTag);
                tagNames.Add(tagName);
            }
        }

        _context.Notes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new NoteDto(
            note.Id,
            note.WorkspaceId,
            note.PageId,
            pageTitle,
            note.Title,
            note.Content,
            note.ContentType,
            note.IsPinned,
            note.CreatedAtUtc,
            note.UpdatedAtUtc,
            tagNames));
    }

    public async Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var accessCheck = await ValidateWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return Result.Failure<NoteDto>(accessCheck.Error);
        }

        var note = await _context.Notes
            .Include(n => n.Page)
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == noteId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

        if (note is null)
        {
            return Result.Failure<NoteDto>(Error.NotFound);
        }

        string? pageTitle = note.Page?.Title;
        if (request.PageId.HasValue && request.PageId != note.PageId)
        {
            var page = await _context.Pages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PageId.Value && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

            if (page is null)
            {
                return Result.Failure<NoteDto>(new Error("Notes.InvalidPage", "The specified page does not exist in this workspace."));
            }

            pageTitle = page.Title;
        }
        else if (!request.PageId.HasValue)
        {
            pageTitle = null;
        }

        note.Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled Note" : request.Title.Trim();
        note.Content = request.Content ?? string.Empty;
        note.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "markdown" : request.ContentType.Trim().ToLowerInvariant();
        note.IsPinned = request.IsPinned;
        note.PageId = request.PageId;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.UpdatedBy = _currentUserService.UserId?.ToString();

        // Update tags if provided
        if (request.Tags != null)
        {
            note.Tags.Clear();
            var cleanTags = request.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList();
            var existingTags = await _context.Tags
                .Where(t => t.WorkspaceId == workspaceId && cleanTags.Contains(t.Name) && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var tag in existingTags)
            {
                note.Tags.Add(tag);
            }

            foreach (var tagName in cleanTags.Except(existingTags.Select(t => t.Name)))
            {
                var newTag = new Tag
                {
                    WorkspaceId = workspaceId,
                    Name = tagName,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = _currentUserService.UserId?.ToString()
                };
                _context.Tags.Add(newTag);
                note.Tags.Add(newTag);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new NoteDto(
            note.Id,
            note.WorkspaceId,
            note.PageId,
            pageTitle,
            note.Title,
            note.Content,
            note.ContentType,
            note.IsPinned,
            note.CreatedAtUtc,
            note.UpdatedAtUtc,
            note.Tags.Where(t => !t.IsDeleted).Select(t => t.Name).ToList()));
    }

    public async Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        var accessCheck = await ValidateWorkspaceAccessAsync(workspaceId, cancellationToken);
        if (!accessCheck.IsSuccess)
        {
            return accessCheck;
        }

        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

        if (note is null)
        {
            return Result.Failure(Error.NotFound);
        }

        note.IsDeleted = true;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.UpdatedBy = _currentUserService.UserId?.ToString();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
