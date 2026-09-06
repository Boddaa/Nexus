using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public class KnowledgeAssessmentService : IKnowledgeAssessmentService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly StudyOptions _options;
    private readonly ILogger<KnowledgeAssessmentService> _logger;

    public KnowledgeAssessmentService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IOptions<StudyOptions> options,
        ILogger<KnowledgeAssessmentService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _options = options?.Value ?? new StudyOptions();
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

    public async Task<Result<TopicPerformanceDto>> GetTopicPerformanceAsync(
        Guid workspaceId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<TopicPerformanceDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<TopicPerformanceDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var topic = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.Id == topicId && t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted)
            .Select(t => new { t.Id, t.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (topic == null)
        {
            return Result.Failure<TopicPerformanceDto>(new Error("Topic.NotFound", "Study topic not found."));
        }

        var now = DateTime.UtcNow;

        var cardStat = await _context.Flashcards
            .AsNoTracking()
            .Where(f => f.StudyTopicId == topicId && f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted)
            .GroupBy(f => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Due = g.Count(f => f.NextReviewDateUtc <= now),
                Reviewed = g.Count(f => f.ReviewCount > 0),
                TotalReviews = g.Sum(f => f.ReviewCount),
                TotalCorrect = g.Sum(f => f.CorrectCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalQuizzes = await _context.Quizzes
            .AsNoTracking()
            .CountAsync(q => q.StudyTopicId == topicId && q.WorkspaceId == workspaceId && q.UserId == userId.Value && !q.IsDeleted, cancellationToken);

        var attemptStat = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.Quiz.StudyTopicId == topicId && a.WorkspaceId == workspaceId && a.UserId == userId.Value && a.IsCompleted && !a.IsDeleted)
            .GroupBy(a => 1)
            .Select(g => new
            {
                Count = g.Count(),
                AvgScore = g.Average(a => a.ScorePercentage)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var performance = CalculatePerformance(
            topic.Id,
            topic.Title,
            cardStat?.Total ?? 0,
            cardStat?.Due ?? 0,
            cardStat?.Reviewed ?? 0,
            cardStat?.TotalReviews ?? 0,
            cardStat?.TotalCorrect ?? 0,
            totalQuizzes,
            attemptStat?.Count ?? 0,
            attemptStat?.AvgScore ?? 0.0
        );

        return Result.Success(performance);
    }

    public async Task<Result<KnowledgeAssessmentDto>> GetAssessmentAsync(
        Guid workspaceId,
        Guid? topicId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<KnowledgeAssessmentDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<KnowledgeAssessmentDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var now = DateTime.UtcNow;

        var topicQuery = _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted);

        if (topicId.HasValue)
        {
            topicQuery = topicQuery.Where(t => t.Id == topicId.Value);
        }

        var topics = await topicQuery
            .Select(t => new { t.Id, t.Title })
            .ToListAsync(cancellationToken);

        var cardQuery = _context.Flashcards
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted);

        if (topicId.HasValue)
        {
            cardQuery = cardQuery.Where(f => f.StudyTopicId == topicId.Value);
        }

        var cardStatsList = await cardQuery
            .Where(f => f.StudyTopicId != null)
            .GroupBy(f => f.StudyTopicId!.Value)
            .Select(g => new
            {
                TopicId = g.Key,
                Total = g.Count(),
                Due = g.Count(f => f.NextReviewDateUtc <= now),
                Reviewed = g.Count(f => f.ReviewCount > 0),
                TotalReviews = g.Sum(f => f.ReviewCount),
                TotalCorrect = g.Sum(f => f.CorrectCount)
            })
            .ToListAsync(cancellationToken);

        var cardStatsByTopic = cardStatsList.ToDictionary(x => x.TopicId);

        var quizQuery = _context.Quizzes
            .AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId && q.UserId == userId.Value && !q.IsDeleted);

        if (topicId.HasValue)
        {
            quizQuery = quizQuery.Where(q => q.StudyTopicId == topicId.Value);
        }

        var quizCountsList = await quizQuery
            .Where(q => q.StudyTopicId != null)
            .GroupBy(q => q.StudyTopicId!.Value)
            .Select(g => new { TopicId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var quizCountsByTopic = quizCountsList.ToDictionary(x => x.TopicId, x => x.Count);

        var attemptQuery = _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.WorkspaceId == workspaceId && a.UserId == userId.Value && a.IsCompleted && !a.IsDeleted);

        if (topicId.HasValue)
        {
            attemptQuery = attemptQuery.Where(a => a.Quiz.StudyTopicId == topicId.Value);
        }

        var attemptStatsList = await attemptQuery
            .Where(a => a.Quiz.StudyTopicId != null)
            .GroupBy(a => a.Quiz.StudyTopicId!.Value)
            .Select(g => new
            {
                TopicId = g.Key,
                Count = g.Count(),
                AvgScore = g.Average(a => a.ScorePercentage)
            })
            .ToListAsync(cancellationToken);

        var attemptStatsByTopic = attemptStatsList.ToDictionary(x => x.TopicId);

        var topicPerformances = new List<TopicPerformanceDto>();
        foreach (var t in topics)
        {
            cardStatsByTopic.TryGetValue(t.Id, out var cs);
            quizCountsByTopic.TryGetValue(t.Id, out var qc);
            attemptStatsByTopic.TryGetValue(t.Id, out var att);

            topicPerformances.Add(CalculatePerformance(
                t.Id,
                t.Title,
                cs?.Total ?? 0,
                cs?.Due ?? 0,
                cs?.Reviewed ?? 0,
                cs?.TotalReviews ?? 0,
                cs?.TotalCorrect ?? 0,
                qc,
                att?.Count ?? 0,
                att?.AvgScore ?? 0.0
            ));
        }

        var strongAreas = topicPerformances.Where(p => p.MasteryLevel == "Strong").ToList();
        var weakAreas = topicPerformances.Where(p => p.MasteryLevel == "Weak").ToList();
        var developingAreas = topicPerformances.Where(p => p.MasteryLevel == "Developing").ToList();

        var totalFlashcards = await cardQuery.CountAsync(cancellationToken);
        var dueFlashcards = await cardQuery.CountAsync(f => f.NextReviewDateUtc <= now, cancellationToken);
        var totalQuizzes = await quizQuery.CountAsync(cancellationToken);
        var totalAttempts = await attemptQuery.CountAsync(cancellationToken);

        var workspaceCardTotals = await cardQuery
            .Where(f => f.ReviewCount > 0)
            .GroupBy(f => 1)
            .Select(g => new { TotalReviews = g.Sum(f => f.ReviewCount), TotalCorrect = g.Sum(f => f.CorrectCount) })
            .FirstOrDefaultAsync(cancellationToken);

        var totalReviews = workspaceCardTotals?.TotalReviews ?? 0;
        var totalCorrect = workspaceCardTotals?.TotalCorrect ?? 0;
        var overallAccuracy = totalReviews > 0 ? Math.Round(((double)totalCorrect / totalReviews) * 100.0, 2) : 0.0;

        var overallQuizAvg = totalAttempts > 0 ? Math.Round(await attemptQuery.AverageAsync(a => a.ScorePercentage, cancellationToken), 2) : 0.0;
        var overallMastery = Math.Round(CalculateBlendedMastery(overallAccuracy, overallQuizAvg, totalReviews > 0, totalAttempts > 0), 2);

        var recommendations = new List<string>();
        if (dueFlashcards > 0)
        {
            recommendations.Add($"You have {dueFlashcards} flashcard{(dueFlashcards > 1 ? "s" : "")} due for spaced repetition review.");
        }
        foreach (var weak in weakAreas.Take(3))
        {
            recommendations.Add($"Topic '{weak.TopicTitle}' has a low mastery score ({weak.MasteryPercentage:F0}%). Consider reviewing flashcards or retaking quizzes.");
        }
        if (recommendations.Count == 0 && topicPerformances.Count > 0)
        {
            recommendations.Add("Great job! All topics are in good standing. Keep practicing to maintain high retention.");
        }
        else if (recommendations.Count == 0)
        {
            recommendations.Add("Create study topics and generate flashcards to begin your personalized learning path.");
        }

        return Result.Success(new KnowledgeAssessmentDto(
            WorkspaceId: workspaceId,
            UserId: userId.Value,
            TopicId: topicId,
            OverallMasteryPercentage: overallMastery,
            OverallAccuracy: overallAccuracy,
            TotalFlashcards: totalFlashcards,
            DueFlashcards: dueFlashcards,
            TotalQuizzes: totalQuizzes,
            TotalAttempts: totalAttempts,
            StrongAreas: strongAreas,
            WeakAreas: weakAreas,
            DevelopingAreas: developingAreas,
            RecommendedActions: recommendations
        ));
    }

    public async Task<Result<StudyDashboardDto>> GetDashboardAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<StudyDashboardDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<StudyDashboardDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var now = DateTime.UtcNow;

        var totalTopics = await _context.StudyTopics
            .AsNoTracking()
            .CountAsync(t => t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted, cancellationToken);

        var sessionQuery = _context.StudySessions
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId && s.UserId == userId.Value && !s.IsDeleted);

        var totalSessions = await sessionQuery.CountAsync(cancellationToken);
        var totalStudyMinutes = await sessionQuery.SumAsync(s => s.DurationMinutes, cancellationToken);

        var flashcardQuery = _context.Flashcards
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted);

        var totalFlashcards = await flashcardQuery.CountAsync(cancellationToken);
        var dueFlashcards = await flashcardQuery.CountAsync(f => f.NextReviewDateUtc <= now, cancellationToken);

        var duePreview = await flashcardQuery
            .Where(f => f.NextReviewDateUtc <= now)
            .OrderBy(f => f.NextReviewDateUtc)
            .Take(5)
            .Select(f => new FlashcardDto(
                f.Id, f.WorkspaceId, f.UserId, f.StudyTopicId, f.ConceptId,
                f.FrontText, f.BackText, f.EaseFactor, f.Repetitions, f.IntervalDays,
                f.NextReviewDateUtc, f.ReviewCount, f.CorrectCount, f.WrongCount,
                f.LastReviewedAtUtc, f.Difficulty, f.State,
                f.SourceDocumentId, f.SourceDocumentChunkId, f.SourcePageId, f.SourceNoteId, f.AiGenerationId,
                f.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var quizQuery = _context.Quizzes
            .AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId && q.UserId == userId.Value && !q.IsDeleted);

        var totalQuizzes = await quizQuery.CountAsync(cancellationToken);

        var attemptQuery = _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.WorkspaceId == workspaceId && a.UserId == userId.Value && a.IsCompleted && !a.IsDeleted);

        var completedAttempts = await attemptQuery.CountAsync(cancellationToken);
        var averageQuizScore = completedAttempts > 0 ? Math.Round(await attemptQuery.AverageAsync(a => a.ScorePercentage, cancellationToken), 2) : 0.0;

        var cardReviewStats = await flashcardQuery
            .Where(f => f.ReviewCount > 0)
            .GroupBy(f => 1)
            .Select(g => new { TotalReviews = g.Sum(f => f.ReviewCount), TotalCorrect = g.Sum(f => f.CorrectCount) })
            .FirstOrDefaultAsync(cancellationToken);

        var totalReviews = cardReviewStats?.TotalReviews ?? 0;
        var totalCorrect = cardReviewStats?.TotalCorrect ?? 0;
        var cardAccuracy = totalReviews > 0 ? Math.Round(((double)totalCorrect / totalReviews) * 100.0, 2) : 0.0;
        var overallMastery = Math.Round(CalculateBlendedMastery(cardAccuracy, averageQuizScore, totalReviews > 0, completedAttempts > 0), 2);

        var recentTopics = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted)
            .OrderByDescending(t => t.UpdatedAtUtc ?? t.CreatedAtUtc)
            .Take(5)
            .Select(t => new { t.Id, t.Title })
            .ToListAsync(cancellationToken);

        var recentTopicIds = recentTopics.Select(t => t.Id).ToList();

        var recentCardStatsList = recentTopicIds.Count > 0
            ? await _context.Flashcards
                .AsNoTracking()
                .Where(f => f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted && f.StudyTopicId != null && recentTopicIds.Contains(f.StudyTopicId.Value))
                .GroupBy(f => f.StudyTopicId!.Value)
                .Select(g => new
                {
                    TopicId = g.Key,
                    Total = g.Count(),
                    Due = g.Count(f => f.NextReviewDateUtc <= now),
                    Reviewed = g.Count(f => f.ReviewCount > 0),
                    TotalReviews = g.Sum(f => f.ReviewCount),
                    TotalCorrect = g.Sum(f => f.CorrectCount)
                })
                .ToListAsync(cancellationToken)
            : new();

        var recentCardStats = recentCardStatsList.ToDictionary(x => x.TopicId);

        var recentQuizCountsList = recentTopicIds.Count > 0
            ? await _context.Quizzes
                .AsNoTracking()
                .Where(q => q.WorkspaceId == workspaceId && q.UserId == userId.Value && !q.IsDeleted && q.StudyTopicId != null && recentTopicIds.Contains(q.StudyTopicId.Value))
                .GroupBy(q => q.StudyTopicId!.Value)
                .Select(g => new { TopicId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken)
            : new();

        var recentQuizCounts = recentQuizCountsList.ToDictionary(x => x.TopicId, x => x.Count);

        var recentAttemptStatsList = recentTopicIds.Count > 0
            ? await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => a.WorkspaceId == workspaceId && a.UserId == userId.Value && a.IsCompleted && !a.IsDeleted && a.Quiz.StudyTopicId != null && recentTopicIds.Contains(a.Quiz.StudyTopicId.Value))
                .GroupBy(a => a.Quiz.StudyTopicId!.Value)
                .Select(g => new
                {
                    TopicId = g.Key,
                    Count = g.Count(),
                    AvgScore = g.Average(a => a.ScorePercentage)
                })
                .ToListAsync(cancellationToken)
            : new();

        var recentAttemptStats = recentAttemptStatsList.ToDictionary(x => x.TopicId);

        var recentTopicsPerformance = new List<TopicPerformanceDto>();
        foreach (var rt in recentTopics)
        {
            recentCardStats.TryGetValue(rt.Id, out var cs);
            recentQuizCounts.TryGetValue(rt.Id, out var qc);
            recentAttemptStats.TryGetValue(rt.Id, out var att);

            recentTopicsPerformance.Add(CalculatePerformance(
                rt.Id,
                rt.Title,
                cs?.Total ?? 0,
                cs?.Due ?? 0,
                cs?.Reviewed ?? 0,
                cs?.TotalReviews ?? 0,
                cs?.TotalCorrect ?? 0,
                qc,
                att?.Count ?? 0,
                att?.AvgScore ?? 0.0
            ));
        }

        return Result.Success(new StudyDashboardDto(
            TotalTopics: totalTopics,
            TotalSessions: totalSessions,
            TotalStudyMinutes: totalStudyMinutes,
            TotalFlashcards: totalFlashcards,
            DueFlashcards: dueFlashcards,
            TotalQuizzes: totalQuizzes,
            CompletedAttempts: completedAttempts,
            AverageQuizScore: averageQuizScore,
            OverallMasteryPercentage: overallMastery,
            RecentTopics: recentTopicsPerformance,
            DueFlashcardPreviews: duePreview
        ));
    }

    private TopicPerformanceDto CalculatePerformance(
        Guid topicId,
        string topicTitle,
        int totalCards,
        int dueCards,
        int reviewedCards,
        int totalReviews,
        int totalCorrect,
        int totalQuizzes,
        int completedAttempts,
        double avgQuizScore)
    {
        var cardAccuracy = totalReviews > 0 ? Math.Round(((double)totalCorrect / totalReviews) * 100.0, 2) : 0.0;
        var avgQuizRounded = completedAttempts > 0 ? Math.Round(avgQuizScore, 2) : 0.0;

        var hasCardData = totalReviews > 0;
        var hasQuizData = completedAttempts > 0;

        var mastery = Math.Round(CalculateBlendedMastery(cardAccuracy, avgQuizRounded, hasCardData, hasQuizData), 2);

        string level;
        if (mastery >= _options.StrongMasteryThreshold)
        {
            level = "Strong";
        }
        else if (mastery < _options.DevelopingMasteryThreshold)
        {
            level = "Weak";
        }
        else
        {
            level = "Developing";
        }

        return new TopicPerformanceDto(
            TopicId: topicId,
            TopicTitle: topicTitle,
            TotalFlashcards: totalCards,
            DueFlashcards: dueCards,
            ReviewedFlashcards: reviewedCards,
            FlashcardAccuracy: cardAccuracy,
            TotalQuizzes: totalQuizzes,
            CompletedAttempts: completedAttempts,
            AverageQuizScore: avgQuizRounded,
            MasteryPercentage: mastery,
            MasteryLevel: level
        );
    }

    private static double CalculateBlendedMastery(double cardAccuracy, double quizAverage, bool hasCardData, bool hasQuizData)
    {
        if (hasCardData && hasQuizData)
        {
            return (cardAccuracy * 0.4) + (quizAverage * 0.6);
        }
        if (hasCardData)
        {
            return cardAccuracy;
        }
        if (hasQuizData)
        {
            return quizAverage;
        }
        return 0.0;
    }
}
