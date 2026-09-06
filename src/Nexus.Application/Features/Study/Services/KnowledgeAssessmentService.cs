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
            .Where(t => t.Id == topicId && t.WorkspaceId == workspaceId && !t.IsDeleted)
            .Select(t => new { t.Id, t.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (topic == null)
        {
            return Result.Failure<TopicPerformanceDto>(new Error("Topic.NotFound", "Study topic not found."));
        }

        var flashcards = await _context.Flashcards
            .AsNoTracking()
            .Where(f => f.StudyTopicId == topicId && f.WorkspaceId == workspaceId && !f.IsDeleted)
            .ToListAsync(cancellationToken);

        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.StudyTopicId == topicId && q.WorkspaceId == workspaceId && !q.IsDeleted)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        var attempts = quizzes.Count > 0
            ? await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => quizzes.Contains(a.QuizId) && a.WorkspaceId == workspaceId && a.IsCompleted && !a.IsDeleted)
                .ToListAsync(cancellationToken)
            : new List<Nexus.Domain.Entities.QuizAttempt>();

        var performance = CalculatePerformance(topic.Id, topic.Title, flashcards, quizzes.Count, attempts);
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

        var topics = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.WorkspaceId == workspaceId && !t.IsDeleted)
            .Select(t => new { t.Id, t.Title })
            .ToListAsync(cancellationToken);

        var flashcardQuery = _context.Flashcards
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && !f.IsDeleted);

        if (topicId.HasValue)
        {
            flashcardQuery = flashcardQuery.Where(f => f.StudyTopicId == topicId.Value);
        }

        var allFlashcards = await flashcardQuery.ToListAsync(cancellationToken);

        var quizQuery = _context.Quizzes
            .AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId && !q.IsDeleted);

        if (topicId.HasValue)
        {
            quizQuery = quizQuery.Where(q => q.StudyTopicId == topicId.Value);
        }

        var allQuizzes = await quizQuery.ToListAsync(cancellationToken);
        var quizIds = allQuizzes.Select(q => q.Id).ToHashSet();

        var allAttempts = quizIds.Count > 0
            ? await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => quizIds.Contains(a.QuizId) && a.WorkspaceId == workspaceId && a.IsCompleted && !a.IsDeleted)
                .ToListAsync(cancellationToken)
            : new List<Nexus.Domain.Entities.QuizAttempt>();

        var topicPerformances = new List<TopicPerformanceDto>();
        var relevantTopics = topicId.HasValue ? topics.Where(t => t.Id == topicId.Value).ToList() : topics;

        foreach (var t in relevantTopics)
        {
            var tCards = allFlashcards.Where(f => f.StudyTopicId == t.Id).ToList();
            var tQuizCount = allQuizzes.Count(q => q.StudyTopicId == t.Id);
            var tQuizIds = allQuizzes.Where(q => q.StudyTopicId == t.Id).Select(q => q.Id).ToHashSet();
            var tAttempts = allAttempts.Where(a => tQuizIds.Contains(a.QuizId)).ToList();

            topicPerformances.Add(CalculatePerformance(t.Id, t.Title, tCards, tQuizCount, tAttempts));
        }

        var strongAreas = topicPerformances.Where(p => p.MasteryLevel == "Strong").ToList();
        var weakAreas = topicPerformances.Where(p => p.MasteryLevel == "Weak").ToList();
        var developingAreas = topicPerformances.Where(p => p.MasteryLevel == "Developing").ToList();

        var totalReviews = allFlashcards.Sum(f => f.ReviewCount);
        var totalCorrect = allFlashcards.Sum(f => f.CorrectCount);
        var overallAccuracy = totalReviews > 0 ? Math.Round(((double)totalCorrect / totalReviews) * 100.0, 2) : 0.0;

        double overallMastery;
        if (topicPerformances.Count > 0)
        {
            overallMastery = Math.Round(topicPerformances.Average(tp => tp.MasteryPercentage), 2);
        }
        else
        {
            var avgQuiz = allAttempts.Count > 0 ? allAttempts.Average(a => a.ScorePercentage) : 0.0;
            overallMastery = Math.Round(CalculateBlendedMastery(overallAccuracy, avgQuiz, totalReviews > 0, allAttempts.Count > 0), 2);
        }

        var now = DateTime.UtcNow;
        var dueFlashcards = allFlashcards.Count(f => f.NextReviewDateUtc <= now);

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
            TotalFlashcards: allFlashcards.Count,
            DueFlashcards: dueFlashcards,
            TotalQuizzes: allQuizzes.Count,
            TotalAttempts: allAttempts.Count,
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
            .CountAsync(t => t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);

        var sessions = await _context.StudySessions
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        var totalSessions = sessions.Count;
        var totalStudyMinutes = sessions.Sum(s => s.DurationMinutes);

        var flashcards = await _context.Flashcards
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && !f.IsDeleted)
            .ToListAsync(cancellationToken);

        var totalFlashcards = flashcards.Count;
        var dueFlashcards = flashcards.Count(f => f.NextReviewDateUtc <= now);

        var duePreview = flashcards
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
            .ToList();

        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId && !q.IsDeleted)
            .ToListAsync(cancellationToken);

        var totalQuizzes = quizzes.Count;

        var quizIds = quizzes.Select(q => q.Id).ToHashSet();
        var attempts = quizIds.Count > 0
            ? await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => quizIds.Contains(a.QuizId) && a.WorkspaceId == workspaceId && a.IsCompleted && !a.IsDeleted)
                .ToListAsync(cancellationToken)
            : new List<Nexus.Domain.Entities.QuizAttempt>();

        var completedAttempts = attempts.Count;
        var averageQuizScore = completedAttempts > 0 ? Math.Round(attempts.Average(a => a.ScorePercentage), 2) : 0.0;

        var assessmentResult = await GetAssessmentAsync(workspaceId, null, cancellationToken);
        var overallMastery = assessmentResult.IsSuccess ? assessmentResult.Value.OverallMasteryPercentage : 0.0;

        var recentTopicsPerformance = new List<TopicPerformanceDto>();
        var recentTopics = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.WorkspaceId == workspaceId && !t.IsDeleted)
            .OrderByDescending(t => t.UpdatedAtUtc ?? t.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var rt in recentTopics)
        {
            var tCards = flashcards.Where(f => f.StudyTopicId == rt.Id).ToList();
            var tQuizCount = quizzes.Count(q => q.StudyTopicId == rt.Id);
            var tQuizIds = quizzes.Where(q => q.StudyTopicId == rt.Id).Select(q => q.Id).ToHashSet();
            var tAttempts = attempts.Where(a => tQuizIds.Contains(a.QuizId)).ToList();
            recentTopicsPerformance.Add(CalculatePerformance(rt.Id, rt.Title, tCards, tQuizCount, tAttempts));
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
        IReadOnlyList<Nexus.Domain.Entities.Flashcard> flashcards,
        int totalQuizzes,
        IReadOnlyList<Nexus.Domain.Entities.QuizAttempt> attempts)
    {
        var now = DateTime.UtcNow;
        var totalCards = flashcards.Count;
        var dueCards = flashcards.Count(f => f.NextReviewDateUtc <= now);
        var reviewedCards = flashcards.Count(f => f.ReviewCount > 0);

        var totalReviews = flashcards.Sum(f => f.ReviewCount);
        var totalCorrect = flashcards.Sum(f => f.CorrectCount);
        var cardAccuracy = totalReviews > 0 ? Math.Round(((double)totalCorrect / totalReviews) * 100.0, 2) : 0.0;

        var completedAttempts = attempts.Count;
        var avgQuizScore = completedAttempts > 0 ? Math.Round(attempts.Average(a => a.ScorePercentage), 2) : 0.0;

        var hasCardData = totalReviews > 0;
        var hasQuizData = completedAttempts > 0;

        var mastery = Math.Round(CalculateBlendedMastery(cardAccuracy, avgQuizScore, hasCardData, hasQuizData), 2);

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
            AverageQuizScore: avgQuizScore,
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
