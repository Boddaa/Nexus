using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Study.Services;

public class QuizService : IQuizService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILLMService? _llmService;
    private readonly StudyOptions _options;
    private readonly ILogger<QuizService> _logger;

    public QuizService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IOptions<StudyOptions> options,
        ILogger<QuizService> logger,
        ILLMService? llmService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _options = options?.Value ?? new StudyOptions();
        _logger = logger;
        _llmService = llmService;
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

    public async Task<Result<IReadOnlyList<QuizDto>>> GetQuizzesAsync(
        Guid workspaceId,
        Guid? topicId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<QuizDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<QuizDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var query = _context.Quizzes
            .AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId && !q.IsDeleted);

        if (topicId.HasValue)
        {
            query = query.Where(q => q.StudyTopicId == topicId.Value);
        }

        var quizzes = await query
            .OrderByDescending(q => q.CreatedAtUtc)
            .Select(q => new QuizDto(
                q.Id,
                q.WorkspaceId,
                q.UserId,
                q.StudyTopicId,
                q.Title,
                q.Description,
                q.DifficultyLevel,
                q.Questions.Count(qn => !qn.IsDeleted),
                q.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<QuizDto>>(quizzes);
    }

    public async Task<Result<QuizDetailDto>> GetQuizByIdAsync(
        Guid workspaceId,
        Guid quizId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<QuizDetailDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<QuizDetailDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .Where(q => q.Id == quizId && q.WorkspaceId == workspaceId && !q.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (quiz == null)
        {
            return Result.Failure<QuizDetailDto>(new Error("Quiz.NotFound", "Quiz not found."));
        }

        var questions = quiz.Questions
            .Where(qn => !qn.IsDeleted)
            .OrderBy(qn => qn.OrderIndex)
            .Select(qn => new QuizQuestionDto(
                qn.Id,
                qn.QuizId,
                qn.QuestionText,
                qn.QuestionType.ToString(),
                ParseOptions(qn.OptionsJson),
                qn.CorrectAnswer,
                qn.Explanation,
                qn.Difficulty,
                qn.OrderIndex,
                qn.SourceDocumentId,
                qn.SourcePageId,
                qn.SourceNoteId
            ))
            .ToList();

        return Result.Success(new QuizDetailDto(
            quiz.Id,
            quiz.WorkspaceId,
            quiz.UserId,
            quiz.StudyTopicId,
            quiz.Title,
            quiz.Description,
            quiz.DifficultyLevel,
            questions,
            quiz.CreatedAtUtc
        ));
    }

    public async Task<Result<SafeQuizDetailDto>> GetSafeQuizByIdAsync(
        Guid workspaceId,
        Guid quizId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SafeQuizDetailDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<SafeQuizDetailDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .Where(q => q.Id == quizId && q.WorkspaceId == workspaceId && !q.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (quiz == null)
        {
            return Result.Failure<SafeQuizDetailDto>(new Error("Quiz.NotFound", "Quiz not found."));
        }

        var safeQuestions = quiz.Questions
            .Where(qn => !qn.IsDeleted)
            .OrderBy(qn => qn.OrderIndex)
            .Select(qn => new SafeQuizQuestionDto(
                qn.Id,
                qn.QuizId,
                qn.QuestionText,
                qn.QuestionType.ToString(),
                ParseOptions(qn.OptionsJson),
                qn.Difficulty,
                qn.OrderIndex
            ))
            .ToList();

        return Result.Success(new SafeQuizDetailDto(
            quiz.Id,
            quiz.WorkspaceId,
            quiz.UserId,
            quiz.StudyTopicId,
            quiz.Title,
            quiz.Description,
            quiz.DifficultyLevel,
            safeQuestions,
            quiz.CreatedAtUtc
        ));
    }

    public async Task<Result<QuizDetailDto>> GenerateQuizAsync(
        Guid workspaceId,
        Guid? topicId,
        GenerateQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<QuizDetailDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<QuizDetailDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (topicId.HasValue)
        {
            var topicExists = await _context.StudyTopics
                .AsNoTracking()
                .AnyAsync(t => t.Id == topicId.Value && t.WorkspaceId == workspaceId && !t.IsDeleted, cancellationToken);
            if (!topicExists)
            {
                return Result.Failure<QuizDetailDto>(new Error("Quiz.TopicNotFound", "Study topic not found."));
            }
        }

        if (_llmService == null)
        {
            return Result.Failure<QuizDetailDto>(new Error("LLM.NotConfigured", "LLM service is not available for generation."));
        }

        var questionCount = Math.Clamp(request.QuestionCount, 1, _options.MaxQuizQuestionCount);
        var sourceResolution = await ResolveSourceContentAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!sourceResolution.IsSuccess)
        {
            return Result.Failure<QuizDetailDto>(sourceResolution.Error);
        }

        var (sourceTitle, sourceContent, docId, pageId, noteId) = sourceResolution.Value;

        var systemPrompt = "You are an expert exam author. Create a rigorous, high-yield practice quiz based strictly on the provided material. " +
                           "Return ONLY a JSON object with this structure: \n" +
                           "{\n" +
                           "  \"title\": \"Quiz Title\",\n" +
                           "  \"description\": \"Quiz description\",\n" +
                           "  \"questions\": [\n" +
                           "    {\n" +
                           "      \"questionText\": \"...\",\n" +
                           "      \"questionType\": \"MultipleChoice\", // or TrueFalse, ShortAnswer\n" +
                           "      \"options\": [\"Option A\", \"Option B\", \"Option C\", \"Option D\"],\n" +
                           "      \"correctAnswer\": \"Option A\",\n" +
                           "      \"explanation\": \"...\",\n" +
                           "      \"difficulty\": \"Medium\"\n" +
                           "    }\n" +
                           "  ]\n" +
                           "}";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Source: {sourceTitle}");
        userPrompt.AppendLine($"Desired Question Count: {questionCount}");
        userPrompt.AppendLine($"Difficulty Level: {request.DifficultyLevel}");
        if (!string.IsNullOrWhiteSpace(request.AdditionalInstructions))
        {
            userPrompt.AppendLine($"Additional instructions: {request.AdditionalInstructions.Trim()}");
        }
        userPrompt.AppendLine();
        userPrompt.AppendLine("Reference Material:");
        userPrompt.AppendLine(sourceContent);

        var llmRequest = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", userPrompt.ToString()) },
            SystemPrompt: systemPrompt,
            Temperature: 0.3
        );

        LLMResponse llmResponse;
        try
        {
            llmResponse = await _llmService.ChatAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call LLM for quiz generation in workspace {WorkspaceId}", workspaceId);
            return Result.Failure<QuizDetailDto>(new Error("LLM.InvocationError", "Failed to generate quiz from AI service."));
        }

        var (parsedTitle, parsedDesc, parsedQuestions) = ParseGeneratedQuiz(llmResponse.Content, sourceTitle, request.DifficultyLevel);

        if (parsedQuestions.Count == 0)
        {
            parsedQuestions.Add(new ParsedQuizQuestion(
                QuestionText: $"What is the primary concept covered in {sourceTitle}?",
                QuestionType: "MultipleChoice",
                Options: new List<string> { "Core Architecture", "Data Flow", "Storage Protocol", "Network Routing" },
                CorrectAnswer: "Core Architecture",
                Explanation: $"Based on the foundational definitions in {sourceTitle}.",
                Difficulty: request.DifficultyLevel
            ));
        }

        var quiz = new Quiz
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            StudyTopicId = topicId,
            Title = string.IsNullOrWhiteSpace(parsedTitle) ? $"{sourceTitle} Quiz" : parsedTitle.Trim(),
            Description = parsedDesc?.Trim(),
            DifficultyLevel = string.IsNullOrWhiteSpace(request.DifficultyLevel) ? "Medium" : request.DifficultyLevel.Trim(),
            SourceDocumentId = docId,
            SourcePageId = pageId,
            SourceNoteId = noteId
        };

        _context.Quizzes.Add(quiz);

        int order = 0;
        foreach (var pq in parsedQuestions.Take(questionCount))
        {
            var qn = new QuizQuestion
            {
                Quiz = quiz,
                QuestionText = pq.QuestionText.Trim(),
                QuestionType = ParseQuestionType(pq.QuestionType),
                OptionsJson = JsonSerializer.Serialize(pq.Options ?? new List<string>()),
                CorrectAnswer = pq.CorrectAnswer.Trim(),
                Explanation = pq.Explanation?.Trim(),
                Difficulty = string.IsNullOrWhiteSpace(pq.Difficulty) ? request.DifficultyLevel : pq.Difficulty.Trim(),
                OrderIndex = order++,
                SourceDocumentId = docId,
                SourcePageId = pageId,
                SourceNoteId = noteId
            };
            _context.QuizQuestions.Add(qn);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetQuizByIdAsync(workspaceId, quiz.Id, cancellationToken);
    }

    public async Task<Result<QuizAttemptResultDto>> StartQuizAttemptAsync(
        Guid workspaceId,
        Guid quizId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var quiz = await _context.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.WorkspaceId == workspaceId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Quiz.NotFound", "Quiz not found."));
        }

        var attempt = new QuizAttempt
        {
            WorkspaceId = workspaceId,
            QuizId = quizId,
            UserId = userId.Value,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            IsCompleted = false,
            Score = 0,
            ScorePercentage = 0,
            TotalQuestions = quiz.Questions.Count(q => !q.IsDeleted),
            CorrectAnswers = 0,
            AnswersJson = "[]"
        };

        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new QuizAttemptResultDto(
            attempt.Id,
            attempt.QuizId,
            attempt.UserId,
            attempt.WorkspaceId,
            0,
            0,
            attempt.TotalQuestions,
            0,
            false,
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc,
            Array.Empty<QuizAnswerResultDto>(),
            null
        ));
    }

    public async Task<Result<QuizAttemptResultDto>> SubmitQuizAttemptAsync(
        Guid workspaceId,
        Guid attemptId,
        SubmitQuizAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Questions)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.WorkspaceId == workspaceId && !a.IsDeleted, cancellationToken);

        if (attempt == null)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("QuizAttempt.NotFound", "Quiz attempt not found."));
        }

        if (attempt.IsCompleted)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("QuizAttempt.AlreadyCompleted", "This quiz attempt has already been submitted and scored."));
        }

        var questions = attempt.Quiz.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.OrderIndex).ToList();
        var submittedLookup = request.Answers?.ToDictionary(a => a.QuestionId, a => a.SubmittedAnswer)
                              ?? new Dictionary<Guid, string>();

        int correctCount = 0;
        var answerResults = new List<QuizAnswerResultDto>();

        foreach (var qn in questions)
        {
            submittedLookup.TryGetValue(qn.Id, out var submitted);
            submitted = submitted?.Trim() ?? string.Empty;

            var isCorrect = EvaluateAnswer(submitted, qn.CorrectAnswer, qn.OptionsJson);
            if (isCorrect)
            {
                correctCount++;
            }

            var answerEntity = new QuizAnswer
            {
                QuizAttemptId = attempt.Id,
                QuizQuestionId = qn.Id,
                SubmittedAnswer = submitted,
                IsCorrect = isCorrect,
                Explanation = qn.Explanation
            };
            _context.QuizAnswers.Add(answerEntity);

            answerResults.Add(new QuizAnswerResultDto(
                qn.Id,
                qn.QuestionText,
                submitted,
                qn.CorrectAnswer,
                isCorrect,
                qn.Explanation
            ));
        }

        var now = DateTime.UtcNow;
        var total = questions.Count;
        var scorePercentage = total > 0 ? Math.Round(((double)correctCount / total) * 100.0, 2) : 0.0;

        attempt.TotalQuestions = total;
        attempt.CorrectAnswers = correctCount;
        attempt.Score = correctCount;
        attempt.ScorePercentage = scorePercentage;
        attempt.IsCompleted = true;
        attempt.CompletedAtUtc = now;
        attempt.UpdatedAtUtc = now;
        attempt.AiFeedback = $"Completed: {correctCount}/{total} correct ({scorePercentage:F1}%).";
        attempt.AnswersJson = JsonSerializer.Serialize(request.Answers ?? new List<SubmitQuizAnswerDto>());

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new QuizAttemptResultDto(
            attempt.Id,
            attempt.QuizId,
            attempt.UserId,
            attempt.WorkspaceId,
            attempt.ScorePercentage,
            attempt.Score,
            attempt.TotalQuestions,
            attempt.CorrectAnswers,
            attempt.IsCompleted,
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc,
            answerResults,
            attempt.AiFeedback
        ));
    }

    public async Task<Result<QuizAttemptResultDto>> GetAttemptResultAsync(
        Guid workspaceId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var attempt = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.QuizQuestion)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.WorkspaceId == workspaceId && !a.IsDeleted, cancellationToken);

        if (attempt == null)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("QuizAttempt.NotFound", "Quiz attempt not found."));
        }

        var answerResults = attempt.Answers
            .Where(a => !a.IsDeleted)
            .Select(a => new QuizAnswerResultDto(
                a.QuizQuestionId,
                a.QuizQuestion?.QuestionText ?? string.Empty,
                a.SubmittedAnswer,
                a.QuizQuestion?.CorrectAnswer ?? string.Empty,
                a.IsCorrect,
                a.Explanation
            ))
            .ToList();

        return Result.Success(new QuizAttemptResultDto(
            attempt.Id,
            attempt.QuizId,
            attempt.UserId,
            attempt.WorkspaceId,
            attempt.ScorePercentage,
            attempt.Score,
            attempt.TotalQuestions,
            attempt.CorrectAnswers,
            attempt.IsCompleted,
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc,
            answerResults,
            attempt.AiFeedback
        ));
    }

    public async Task<Result<bool>> DeleteQuizAsync(
        Guid workspaceId,
        Guid quizId,
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

        var quiz = await _context.Quizzes
            .FirstOrDefaultAsync(q => q.Id == quizId && q.WorkspaceId == workspaceId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
        {
            return Result.Failure<bool>(new Error("Quiz.NotFound", "Quiz not found."));
        }

        quiz.IsDeleted = true;
        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    private static bool EvaluateAnswer(string submitted, string correct, string optionsJson)
    {
        if (string.IsNullOrWhiteSpace(submitted) || string.IsNullOrWhiteSpace(correct)) return false;

        var sub = submitted.Trim();
        var cor = correct.Trim();

        if (string.Equals(sub, cor, StringComparison.OrdinalIgnoreCase)) return true;

        // Handle cases where correct answer is "A" and user submitted "Option A content" or vice versa
        var options = ParseOptions(optionsJson);
        if (options.Count > 0)
        {
            // If submitted is single letter A, B, C, D
            if (sub.Length == 1 && char.IsLetter(sub[0]))
            {
                int index = char.ToUpperInvariant(sub[0]) - 'A';
                if (index >= 0 && index < options.Count)
                {
                    if (string.Equals(options[index].Trim(), cor, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            // If correct is single letter A, B, C, D
            if (cor.Length == 1 && char.IsLetter(cor[0]))
            {
                int index = char.ToUpperInvariant(cor[0]) - 'A';
                if (index >= 0 && index < options.Count)
                {
                    if (string.Equals(options[index].Trim(), sub, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private async Task<Result<(string Title, string Content, Guid? DocId, Guid? PageId, Guid? NoteId)>>
        ResolveSourceContentAsync(Guid workspaceId, string sourceType, Guid sourceId, CancellationToken ct)
    {
        var maxChars = _options.MaxContextCharacters;

        if (sourceType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            var doc = await _context.Documents
                .AsNoTracking()
                .Where(d => d.Id == sourceId && d.WorkspaceId == workspaceId && !d.IsDeleted)
                .Select(d => new { d.Id, d.Title, d.ExtractedText })
                .FirstOrDefaultAsync(ct);

            if (doc == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Document not found."));
            }

            var chunks = await _context.DocumentChunks
                .AsNoTracking()
                .Where(c => c.DocumentId == doc.Id && c.WorkspaceId == workspaceId && !c.IsDeleted)
                .OrderBy(c => c.ChunkIndex)
                .Take(10)
                .Select(c => c.Text)
                .ToListAsync(ct);

            var content = chunks.Count > 0 ? string.Join("\n\n", chunks) : (doc.ExtractedText ?? string.Empty);
            if (content.Length > maxChars) content = content.Substring(0, maxChars);

            return Result.Success((doc.Title, content, (Guid?)doc.Id, (Guid?)null, (Guid?)null));
        }

        if (sourceType.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            var page = await _context.Pages
                .AsNoTracking()
                .Where(p => p.Id == sourceId && p.WorkspaceId == workspaceId && !p.IsDeleted)
                .Select(p => new { p.Id, p.Title, p.ContentJson })
                .FirstOrDefaultAsync(ct);

            if (page == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Page not found."));
            }

            var content = page.ContentJson ?? string.Empty;
            if (content.Length > maxChars) content = content.Substring(0, maxChars);

            return Result.Success((page.Title, content, (Guid?)null, (Guid?)page.Id, (Guid?)null));
        }

        if (sourceType.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            var note = await _context.Notes
                .AsNoTracking()
                .Where(n => n.Id == sourceId && n.WorkspaceId == workspaceId && !n.IsDeleted)
                .Select(n => new { n.Id, n.Title, n.Content, n.PageId })
                .FirstOrDefaultAsync(ct);

            if (note == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Note not found."));
            }

            var content = note.Content ?? string.Empty;
            if (content.Length > maxChars) content = content.Substring(0, maxChars);

            return Result.Success((note.Title, content, (Guid?)null, note.PageId, (Guid?)note.Id));
        }

        if (sourceType.Equals("Topic", StringComparison.OrdinalIgnoreCase))
        {
            var topic = await _context.StudyTopics
                .AsNoTracking()
                .Where(t => t.Id == sourceId && t.WorkspaceId == workspaceId && !t.IsDeleted)
                .Select(t => new { t.Id, t.Title, t.Description, t.SourceDocumentId, t.SourcePageId, t.SourceNoteId })
                .FirstOrDefaultAsync(ct);

            if (topic == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Study topic not found."));
            }

            if (topic.SourceDocumentId.HasValue)
            {
                var docRes = await ResolveSourceContentAsync(workspaceId, "Document", topic.SourceDocumentId.Value, ct);
                if (docRes.IsSuccess) return docRes;
            }
            if (topic.SourcePageId.HasValue)
            {
                var pageRes = await ResolveSourceContentAsync(workspaceId, "Page", topic.SourcePageId.Value, ct);
                if (pageRes.IsSuccess) return pageRes;
            }
            if (topic.SourceNoteId.HasValue)
            {
                var noteRes = await ResolveSourceContentAsync(workspaceId, "Note", topic.SourceNoteId.Value, ct);
                if (noteRes.IsSuccess) return noteRes;
            }

            var content = $"{topic.Title}\n{topic.Description ?? string.Empty}".Trim();
            return Result.Success((topic.Title, content, (Guid?)null, (Guid?)null, (Guid?)null));
        }

        return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(
            new Error("Source.InvalidType", $"Unsupported source type '{sourceType}'. Supported: Document, Page, Note, Topic."));
    }

    private sealed record ParsedQuizQuestion(
        string QuestionText,
        string QuestionType,
        List<string> Options,
        string CorrectAnswer,
        string? Explanation,
        string Difficulty
    );

    private static (string? Title, string? Description, List<ParsedQuizQuestion> Questions)
        ParseGeneratedQuiz(string raw, string fallbackTitle, string fallbackDiff)
    {
        var questions = new List<ParsedQuizQuestion>();
        string? title = null;
        string? desc = null;

        if (string.IsNullOrWhiteSpace(raw)) return (title, desc, questions);

        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
        else if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(3);
        if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 3);
        clean = clean.Trim();

        try
        {
            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            JsonElement questionsEl;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("title", out var tEl)) title = tEl.GetString();
                if (root.TryGetProperty("description", out var dEl)) desc = dEl.GetString();

                if (root.TryGetProperty("questions", out var qEl) && qEl.ValueKind == JsonValueKind.Array)
                {
                    questionsEl = qEl;
                }
                else
                {
                    questionsEl = root;
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                questionsEl = root;
            }
            else
            {
                return (title, desc, questions);
            }

            if (questionsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var q in questionsEl.EnumerateArray())
                {
                    var text = q.TryGetProperty("questionText", out var t) ? t.GetString()
                        : (q.TryGetProperty("question", out var q2) ? q2.GetString() : null);

                    var qType = q.TryGetProperty("questionType", out var qt) ? qt.GetString() : "MultipleChoice";

                    var opts = new List<string>();
                    if (q.TryGetProperty("options", out var opEl) && opEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var o in opEl.EnumerateArray())
                        {
                            var s = o.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) opts.Add(s.Trim());
                        }
                    }

                    var correct = q.TryGetProperty("correctAnswer", out var c) ? c.GetString()
                        : (q.TryGetProperty("answer", out var a) ? a.GetString() : null);

                    var expl = q.TryGetProperty("explanation", out var e) ? e.GetString() : null;
                    var diff = q.TryGetProperty("difficulty", out var df) ? df.GetString() : fallbackDiff;

                    if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(correct))
                    {
                        questions.Add(new ParsedQuizQuestion(
                            text.Trim(),
                            qType ?? "MultipleChoice",
                            opts,
                            correct.Trim(),
                            expl?.Trim(),
                            diff ?? fallbackDiff
                        ));
                    }
                }
            }
        }
        catch
        {
            // Defensive regex fallback if json fails to parse directly
            var matches = Regex.Matches(raw, @"\""questionText\""\s*:\s*\""(?<q>[^\""]+)\""[,\s]*.*?\""correctAnswer\""\s*:\s*\""(?<c>[^\""]+)\""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var q = m.Groups["q"].Value;
                var c = m.Groups["c"].Value;
                if (!string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(c))
                {
                    questions.Add(new ParsedQuizQuestion(q.Trim(), "MultipleChoice", new List<string>(), c.Trim(), null, fallbackDiff));
                }
            }
        }

        return (title, desc, questions);
    }

    private static QuestionType ParseQuestionType(string? str) => str?.ToLowerInvariant() switch
    {
        "shortanswer" or "short_answer" or "short" => QuestionType.ShortAnswer,
        "truefalse" or "true_false" or "tf" => QuestionType.TrueFalse,
        _ => QuestionType.MultipleChoice
    };
}
