using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.Study.Services;

public class StudyTopicService : IStudyTopicService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<StudyTopicService> _logger;

    public StudyTopicService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<StudyTopicService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return false;

        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                           (w.OwnerId == userId.Value || w.Members.Any(m => m.UserId == userId.Value && !m.IsDeleted)),
                      cancellationToken);
    }

    public async Task<Result<IReadOnlyList<StudyTopicDto>>> GetTopicsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<StudyTopicDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<StudyTopicDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var topics = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.WorkspaceId == workspaceId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new StudyTopicDto(
                t.Id,
                t.WorkspaceId,
                t.UserId,
                t.Title,
                t.Description,
                t.SourceDocumentId,
                t.SourcePageId,
                t.SourceNoteId,
                t.Flashcards.Count(f => !f.IsDeleted),
                t.Quizzes.Count(q => !q.IsDeleted),
                t.CreatedAtUtc,
                t.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StudyTopicDto>>(topics);
    }

    public async Task<Result<StudyTopicDto>> GetTopicByIdAsync(
        Guid workspaceId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudyTopicDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudyTopicDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var topic = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.Id == topicId && t.WorkspaceId == workspaceId && !t.IsDeleted)
            .Select(t => new StudyTopicDto(
                t.Id,
                t.WorkspaceId,
                t.UserId,
                t.Title,
                t.Description,
                t.SourceDocumentId,
                t.SourcePageId,
                t.SourceNoteId,
                t.Flashcards.Count(f => !f.IsDeleted),
                t.Quizzes.Count(q => !q.IsDeleted),
                t.CreatedAtUtc,
                t.UpdatedAtUtc
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (topic == null)
        {
            return Result.Failure<StudyTopicDto>(new Error("StudyTopic.NotFound", "Study topic not found."));
        }

        return Result.Success(topic);
    }

    public async Task<Result<StudyTopicDto>> CreateTopicAsync(
        Guid workspaceId,
        CreateStudyTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudyTopicDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudyTopicDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            return Result.Failure<StudyTopicDto>(new Error("StudyTopic.Validation", "Topic title is required."));
        }

        if (request.SourceDocumentId.HasValue)
        {
            var docExists = await _context.Documents
                .AsNoTracking()
                .AnyAsync(d => d.Id == request.SourceDocumentId.Value && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);
            if (!docExists)
            {
                return Result.Failure<StudyTopicDto>(new Error("StudyTopic.InvalidSource", "Source document not found in this workspace."));
            }
        }

        if (request.SourcePageId.HasValue)
        {
            var pageExists = await _context.Pages
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.SourcePageId.Value && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);
            if (!pageExists)
            {
                return Result.Failure<StudyTopicDto>(new Error("StudyTopic.InvalidSource", "Source page not found in this workspace."));
            }
        }

        if (request.SourceNoteId.HasValue)
        {
            var noteExists = await _context.Notes
                .AsNoTracking()
                .AnyAsync(n => n.Id == request.SourceNoteId.Value && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);
            if (!noteExists)
            {
                return Result.Failure<StudyTopicDto>(new Error("StudyTopic.InvalidSource", "Source note not found in this workspace."));
            }
        }

        var topic = new StudyTopic
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            SourceDocumentId = request.SourceDocumentId,
            SourcePageId = request.SourcePageId,
            SourceNoteId = request.SourceNoteId
        };

        _context.StudyTopics.Add(topic);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new StudyTopicDto(
            topic.Id,
            topic.WorkspaceId,
            topic.UserId,
            topic.Title,
            topic.Description,
            topic.SourceDocumentId,
            topic.SourcePageId,
            topic.SourceNoteId,
            0,
            0,
            topic.CreatedAtUtc,
            topic.UpdatedAtUtc
        ));
    }

    public async Task<Result<StudyTopicDto>> UpdateTopicAsync(
        Guid workspaceId,
        Guid topicId,
        UpdateStudyTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudyTopicDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudyTopicDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            return Result.Failure<StudyTopicDto>(new Error("StudyTopic.Validation", "Topic title cannot be empty."));
        }

        var topic = await _context.StudyTopics
            .Include(t => t.Flashcards)
            .Include(t => t.Quizzes)
            .FirstOrDefaultAsync(t => t.Id == topicId && t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);

        if (topic == null)
        {
            return Result.Failure<StudyTopicDto>(new Error("StudyTopic.NotFound", "Study topic not found."));
        }

        topic.Title = request.Title.Trim();
        topic.Description = request.Description?.Trim();
        topic.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new StudyTopicDto(
            topic.Id,
            topic.WorkspaceId,
            topic.UserId,
            topic.Title,
            topic.Description,
            topic.SourceDocumentId,
            topic.SourcePageId,
            topic.SourceNoteId,
            topic.Flashcards.Count(f => !f.IsDeleted),
            topic.Quizzes.Count(q => !q.IsDeleted),
            topic.CreatedAtUtc,
            topic.UpdatedAtUtc
        ));
    }

    public async Task<Result<bool>> DeleteTopicAsync(
        Guid workspaceId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<bool>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<bool>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var topic = await _context.StudyTopics
            .FirstOrDefaultAsync(t => t.Id == topicId && t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);

        if (topic == null)
        {
            return Result.Failure<bool>(new Error("StudyTopic.NotFound", "Study topic not found."));
        }

        topic.IsDeleted = true;
        topic.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
