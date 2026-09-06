using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Study.Services;

public class StudySessionService : IStudySessionService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<StudySessionService> _logger;

    public StudySessionService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ILogger<StudySessionService> logger)
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

    public async Task<Result<IReadOnlyList<StudySessionDto>>> GetSessionsAsync(
        Guid workspaceId,
        Guid? topicId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<StudySessionDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<StudySessionDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var query = _context.StudySessions
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId && s.UserId == userId.Value && !s.IsDeleted);

        if (topicId.HasValue)
        {
            query = query.Where(s => s.StudyTopicId == topicId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.StartedAtUtc)
            .Select(s => new StudySessionDto(
                s.Id,
                s.WorkspaceId,
                s.UserId,
                s.StudyTopicId,
                s.Title,
                s.StartedAtUtc,
                s.EndedAtUtc,
                s.CompletedAtUtc,
                s.DurationMinutes,
                s.Status,
                s.ItemsAttempted,
                s.ItemsCompleted,
                s.Notes
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StudySessionDto>>(sessions);
    }

    public async Task<Result<StudySessionDto>> GetSessionByIdAsync(
        Guid workspaceId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudySessionDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudySessionDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var session = await _context.StudySessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.WorkspaceId == workspaceId && s.UserId == userId.Value && !s.IsDeleted)
            .Select(s => new StudySessionDto(
                s.Id,
                s.WorkspaceId,
                s.UserId,
                s.StudyTopicId,
                s.Title,
                s.StartedAtUtc,
                s.EndedAtUtc,
                s.CompletedAtUtc,
                s.DurationMinutes,
                s.Status,
                s.ItemsAttempted,
                s.ItemsCompleted,
                s.Notes
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            return Result.Failure<StudySessionDto>(new Error("StudySession.NotFound", "Study session not found."));
        }

        return Result.Success(session);
    }

    public async Task<Result<StudySessionDto>> StartSessionAsync(
        Guid workspaceId,
        Guid? topicId,
        StartStudySessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudySessionDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudySessionDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (topicId.HasValue)
        {
            var topicExists = await _context.StudyTopics
                .AsNoTracking()
                .AnyAsync(t => t.Id == topicId.Value && t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);
            if (!topicExists)
            {
                return Result.Failure<StudySessionDto>(new Error("StudySession.TopicNotFound", "Study topic not found."));
            }
        }

        var title = string.IsNullOrWhiteSpace(request?.Title) ? "Study Session" : request.Title.Trim();

        var session = new StudySession
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            StudyTopicId = topicId,
            Title = title,
            StartedAtUtc = DateTime.UtcNow,
            Status = StudySessionStatus.InProgress,
            ItemsAttempted = 0,
            ItemsCompleted = 0,
            Notes = request?.Notes?.Trim()
        };

        _context.StudySessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new StudySessionDto(
            session.Id,
            session.WorkspaceId,
            session.UserId,
            session.StudyTopicId,
            session.Title,
            session.StartedAtUtc,
            session.EndedAtUtc,
            session.CompletedAtUtc,
            session.DurationMinutes,
            session.Status,
            session.ItemsAttempted,
            session.ItemsCompleted,
            session.Notes
        ));
    }

    public async Task<Result<StudySessionDto>> CompleteSessionAsync(
        Guid workspaceId,
        Guid sessionId,
        CompleteStudySessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudySessionDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudySessionDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var session = await _context.StudySessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.WorkspaceId == workspaceId && s.UserId == userId.Value && !s.IsDeleted, cancellationToken);

        if (session == null)
        {
            return Result.Failure<StudySessionDto>(new Error("StudySession.NotFound", "Study session not found."));
        }

        var now = DateTime.UtcNow;
        session.CompletedAtUtc = now;
        session.EndedAtUtc = now;
        session.Status = StudySessionStatus.Completed;

        var computedDuration = (int)Math.Max(1, Math.Round((now - session.StartedAtUtc).TotalMinutes));
        session.DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : computedDuration;

        if (request.ItemsAttempted > 0)
        {
            session.ItemsAttempted = request.ItemsAttempted;
        }

        if (request.ItemsCompleted > 0)
        {
            session.ItemsCompleted = request.ItemsCompleted;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                ? request.Notes.Trim()
                : $"{session.Notes}\n{request.Notes.Trim()}";
        }

        session.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new StudySessionDto(
            session.Id,
            session.WorkspaceId,
            session.UserId,
            session.StudyTopicId,
            session.Title,
            session.StartedAtUtc,
            session.EndedAtUtc,
            session.CompletedAtUtc,
            session.DurationMinutes,
            session.Status,
            session.ItemsAttempted,
            session.ItemsCompleted,
            session.Notes
        ));
    }
}
