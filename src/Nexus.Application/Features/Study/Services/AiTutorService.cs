using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Study.Services;

public class AiTutorService : IAiTutorService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILLMService? _llmService;
    private readonly IRagService? _ragService;
    private readonly IKnowledgeAssessmentService _assessmentService;
    private readonly StudyOptions _options;
    private readonly ILogger<AiTutorService> _logger;

    public AiTutorService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IKnowledgeAssessmentService assessmentService,
        IOptions<StudyOptions> options,
        ILogger<AiTutorService> logger,
        ILLMService? llmService = null,
        IRagService? ragService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _assessmentService = assessmentService;
        _options = options?.Value ?? new StudyOptions();
        _logger = logger;
        _llmService = llmService;
        _ragService = ragService;
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

    public async Task<Result<TutorResponseDto>> ChatWithTutorAsync(
        Guid workspaceId,
        TutorChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<TutorResponseDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<TutorResponseDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return Result.Failure<TutorResponseDto>(new Error("Tutor.Validation", "Message cannot be empty."));
        }

        if (_llmService == null)
        {
            return Result.Failure<TutorResponseDto>(new Error("LLM.NotConfigured", "AI service is not configured."));
        }

        // 1. Resolve or create AiConversation with ContextType.Study
        AiConversation? conversation;
        if (request.ConversationId.HasValue)
        {
            conversation = await _context.AiConversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.WorkspaceId == workspaceId && !c.IsDeleted, cancellationToken);

            if (conversation == null)
            {
                return Result.Failure<TutorResponseDto>(new Error("Conversation.NotFound", "Study conversation not found."));
            }
        }
        else
        {
            // Check if there is an existing active study conversation for this topic
            var existing = await _context.AiConversations
                .Include(c => c.Messages)
                .Where(c => c.WorkspaceId == workspaceId && c.UserId == userId.Value && c.ContextType == ContextType.Study && c.ContextEntityId == request.StudyTopicId && !c.IsDeleted)
                .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                conversation = existing;
            }
            else
            {
                string title = "AI Tutor Session";
                if (request.StudyTopicId.HasValue)
                {
                    var topicTitle = await _context.StudyTopics
                        .Where(t => t.Id == request.StudyTopicId.Value)
                        .Select(t => t.Title)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(topicTitle))
                    {
                        title = $"Tutor: {topicTitle}";
                    }
                }

                conversation = new AiConversation
                {
                    WorkspaceId = workspaceId,
                    UserId = userId.Value,
                    Title = title,
                    ContextType = ContextType.Study,
                    ContextEntityId = request.StudyTopicId,
                    IsArchived = false
                };

                _context.AiConversations.Add(conversation);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // 2. Fetch context: topic performance & RAG sources
        var sources = new List<ChatSourceDto>();
        string performanceSnippet = string.Empty;

        if (request.StudyTopicId.HasValue)
        {
            var perfResult = await _assessmentService.GetTopicPerformanceAsync(workspaceId, request.StudyTopicId.Value, cancellationToken);
            if (perfResult.IsSuccess)
            {
                var p = perfResult.Value;
                performanceSnippet = $"Topic: {p.TopicTitle} | Mastery: {p.MasteryPercentage:F0}% ({p.MasteryLevel}) | Card Accuracy: {p.FlashcardAccuracy:F0}% | Quiz Average: {p.AverageQuizScore:F0}% | Due Cards: {p.DueFlashcards}";
            }
        }

        // 3. Ground with RAG if available
        if (_ragService != null)
        {
            try
            {
                var ragResult = await _ragService.AnswerQuestionAsync(workspaceId, request.Message, null, cancellationToken);
                if (ragResult.IsSuccess && ragResult.Value?.Sources != null)
                {
                    sources.AddRange(ragResult.Value.Sources.Take(_options.DefaultTutorMaxSources));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG retrieval for tutor chat encountered an issue; proceeding with direct context.");
            }
        }

        // 4. Build prompt
        var systemSb = new StringBuilder();
        systemSb.AppendLine("You are the NEXUS AI Study Tutor, an expert personal tutor and mentor.");
        systemSb.AppendLine("Your goal is to help the student learn with deep understanding through pedagogical explanations, active recall questions, and clear step-by-step guidance.");
        systemSb.AppendLine("Format your response with clean markdown. At the very end of your response, output two JSON arrays formatted exactly as:");
        systemSb.AppendLine("---KEY_TAKEAWAYS---");
        systemSb.AppendLine("[\"point 1\", \"point 2\"]");
        systemSb.AppendLine("---FOLLOW_UP_SUGGESTIONS---");
        systemSb.AppendLine("[\"suggestion 1\", \"suggestion 2\"]");

        if (!string.IsNullOrWhiteSpace(performanceSnippet))
        {
            systemSb.AppendLine($"Student Performance Context: {performanceSnippet}");
        }

        var messageHistory = new List<LLMChatMessage>();
        var priorMessages = conversation.Messages
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.CreatedAtUtc)
            .TakeLast(6)
            .ToList();

        foreach (var m in priorMessages)
        {
            var roleStr = m.Role == AiRole.User ? "user" : "assistant";
            messageHistory.Add(new LLMChatMessage(roleStr, m.Content));
        }

        messageHistory.Add(new LLMChatMessage("user", request.Message));

        var llmRequest = new LLMRequest(
            Messages: messageHistory,
            SystemPrompt: systemSb.ToString(),
            Temperature: 0.5
        );

        LLMResponse llmResponse;
        try
        {
            llmResponse = await _llmService.ChatAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call LLM for AI Tutor chat.");
            return Result.Failure<TutorResponseDto>(new Error("LLM.InvocationError", "Failed to communicate with AI Tutor service."));
        }

        var (cleanAnswer, takeaways, followUps) = ParseTutorResponse(llmResponse.Content);

        // 5. Persist user message and assistant message
        var now = DateTime.UtcNow;
        var userMsg = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiRole.User,
            Content = request.Message,
            CreatedAtUtc = now
        };

        var assistantMsg = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiRole.Assistant,
            Content = cleanAnswer,
            CreatedAtUtc = now.AddMilliseconds(100)
        };

        _context.AiMessages.Add(userMsg);
        _context.AiMessages.Add(assistantMsg);

        conversation.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new TutorResponseDto(
            ConversationId: conversation.Id,
            AssistantMessage: cleanAnswer,
            Sources: sources,
            KeyTakeaways: takeaways,
            FollowUpSuggestions: followUps
        ));
    }

    public async Task<Result<TutorHintDto>> GetHintAsync(
        Guid workspaceId,
        TutorHintRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<TutorHintDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<TutorHintDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (_llmService == null)
        {
            return Result.Failure<TutorHintDto>(new Error("LLM.NotConfigured", "AI service is not configured."));
        }

        var question = await _context.QuizQuestions
            .AsNoTracking()
            .Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId && q.Quiz.WorkspaceId == workspaceId && !q.IsDeleted, cancellationToken);

        if (question == null)
        {
            return Result.Failure<TutorHintDto>(new Error("Question.NotFound", "Quiz question not found."));
        }

        var systemPrompt = "You are a pedagogical AI tutor. The student is answering a question and asked for a hint. " +
                           "Provide a targeted, encouraging hint that points toward the key concept WITHOUT giving away the correct answer or naming the right option. " +
                           "Keep it under 3 sentences.";

        var prompt = $"Question: {question.QuestionText}\n" +
                     $"Options: {question.OptionsJson}\n" +
                     (!string.IsNullOrWhiteSpace(request.CurrentDraftAnswer) ? $"Student's current draft: {request.CurrentDraftAnswer}\n" : string.Empty) +
                     "Give a helpful conceptual hint.";

        var llmRequest = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", prompt) },
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
            _logger.LogError(ex, "Failed to generate hint.");
            return Result.Failure<TutorHintDto>(new Error("LLM.InvocationError", "Failed to generate hint."));
        }

        return Result.Success(new TutorHintDto(
            Hint: llmResponse.Content.Trim(),
            HintLevel: 1
        ));
    }

    public async Task<Result<TutorExplanationDto>> ExplainWrongAnswerAsync(
        Guid workspaceId,
        TutorExplainWrongAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<TutorExplanationDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<TutorExplanationDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (_llmService == null)
        {
            return Result.Failure<TutorExplanationDto>(new Error("LLM.NotConfigured", "AI service is not configured."));
        }

        var question = await _context.QuizQuestions
            .AsNoTracking()
            .Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId && q.Quiz.WorkspaceId == workspaceId && !q.IsDeleted, cancellationToken);

        if (question == null)
        {
            return Result.Failure<TutorExplanationDto>(new Error("Question.NotFound", "Quiz question not found."));
        }

        var systemPrompt = "You are a master tutor analyzing a student's misconception. " +
                           "Explain clearly why their submitted answer was incorrect, clarify the exact misconception, and provide 2-3 specific remedial steps to master this concept. " +
                           "Format as JSON with keys: \"explanation\" (string), \"remedialSteps\" (array of strings).";

        var prompt = $"Question: {question.QuestionText}\n" +
                     $"Submitted Answer: {request.SubmittedAnswer}\n" +
                     $"Correct Answer: {question.CorrectAnswer}\n" +
                     $"Context/Original Explanation: {question.Explanation ?? "None"}";

        var llmRequest = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", prompt) },
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
            _logger.LogError(ex, "Failed to generate explanation for wrong answer.");
            return Result.Failure<TutorExplanationDto>(new Error("LLM.InvocationError", "Failed to generate explanation."));
        }

        var (explanation, steps) = ParseExplanation(llmResponse.Content, question.CorrectAnswer);

        return Result.Success(new TutorExplanationDto(
            Explanation: explanation,
            RemedialSteps: steps,
            Sources: Array.Empty<ChatSourceDto>()
        ));
    }

    public async Task<Result<TutorMiniExerciseDto>> GenerateMiniExerciseAsync(
        Guid workspaceId,
        TutorMiniExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<TutorMiniExerciseDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<TutorMiniExerciseDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (_llmService == null)
        {
            return Result.Failure<TutorMiniExerciseDto>(new Error("LLM.NotConfigured", "AI service is not configured."));
        }

        string targetConcept = request.TargetConcept?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(targetConcept) && request.StudyTopicId.HasValue)
        {
            var topic = await _context.StudyTopics
                .AsNoTracking()
                .Where(t => t.Id == request.StudyTopicId.Value && t.WorkspaceId == workspaceId && !t.IsDeleted)
                .Select(t => t.Title)
                .FirstOrDefaultAsync(cancellationToken);
            targetConcept = topic ?? "General Knowledge";
        }
        else if (string.IsNullOrWhiteSpace(targetConcept))
        {
            var assessment = await _assessmentService.GetAssessmentAsync(workspaceId, null, cancellationToken);
            if (assessment.IsSuccess && assessment.Value.WeakAreas.Count > 0)
            {
                targetConcept = assessment.Value.WeakAreas[0].TopicTitle;
            }
            else
            {
                targetConcept = "Core Principles";
            }
        }

        var systemPrompt = "You are a tutoring AI that generates quick 1-question mini exercises for rapid active recall. " +
                           "Format your output strictly as a JSON object with keys: \n" +
                           "\"question\" (string), \"exerciseType\" (\"MultipleChoice\" or \"ShortAnswer\"), \"options\" (array of strings, or null), \"answer\" (string), \"explanation\" (string).";

        var prompt = $"Target Concept: {targetConcept}\nDifficulty: {request.Difficulty}\nGenerate a high-impact practice exercise.";

        var llmRequest = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", prompt) },
            SystemPrompt: systemPrompt,
            Temperature: 0.4
        );

        LLMResponse llmResponse;
        try
        {
            llmResponse = await _llmService.ChatAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate mini exercise.");
            return Result.Failure<TutorMiniExerciseDto>(new Error("LLM.InvocationError", "Failed to generate mini exercise."));
        }

        var exercise = ParseMiniExercise(llmResponse.Content, targetConcept);
        return Result.Success(exercise);
    }

    private static (string Answer, List<string> Takeaways, List<string> FollowUps) ParseTutorResponse(string raw)
    {
        var takeaways = new List<string>();
        var followUps = new List<string>();
        var answer = raw;

        if (string.IsNullOrWhiteSpace(raw)) return (string.Empty, takeaways, followUps);

        var takeawayMarker = "---KEY_TAKEAWAYS---";
        var followUpMarker = "---FOLLOW_UP_SUGGESTIONS---";

        var takeawayIdx = raw.IndexOf(takeawayMarker, StringComparison.OrdinalIgnoreCase);
        var followUpIdx = raw.IndexOf(followUpMarker, StringComparison.OrdinalIgnoreCase);

        if (takeawayIdx >= 0)
        {
            answer = raw.Substring(0, takeawayIdx).Trim();

            string takeawayJson;
            if (followUpIdx > takeawayIdx)
            {
                takeawayJson = raw.Substring(takeawayIdx + takeawayMarker.Length, followUpIdx - (takeawayIdx + takeawayMarker.Length)).Trim();
                var followUpJson = raw.Substring(followUpIdx + followUpMarker.Length).Trim();
                followUps = ParseJsonStringList(followUpJson);
            }
            else
            {
                takeawayJson = raw.Substring(takeawayIdx + takeawayMarker.Length).Trim();
            }

            takeaways = ParseJsonStringList(takeawayJson);
        }
        else if (followUpIdx >= 0)
        {
            answer = raw.Substring(0, followUpIdx).Trim();
            var followUpJson = raw.Substring(followUpIdx + followUpMarker.Length).Trim();
            followUps = ParseJsonStringList(followUpJson);
        }

        if (takeaways.Count == 0)
        {
            takeaways.Add("Review core definitions and concepts.");
            takeaways.Add("Practice with flashcards to reinforce recall.");
        }

        if (followUps.Count == 0)
        {
            followUps.Add("Can you give me a concrete example?");
            followUps.Add("Test me with a quick practice question.");
        }

        return (answer, takeaways, followUps);
    }

    private static List<string> ParseJsonStringList(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;

        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
        else if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(3);
        if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 3);
        clean = clean.Trim();

        try
        {
            using var doc = JsonDocument.Parse(clean);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var str = el.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) list.Add(str.Trim());
                }
            }
        }
        catch
        {
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                var trimmed = l.Trim().TrimStart('-', '*', '1', '2', '3', '.', ' ');
                if (!string.IsNullOrWhiteSpace(trimmed)) list.Add(trimmed);
            }
        }

        return list;
    }

    private static (string Explanation, List<string> RemedialSteps) ParseExplanation(string raw, string correctAnswer)
    {
        var steps = new List<string>();
        string explanation = raw;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return ($"The correct answer is '{correctAnswer}'.", new List<string> { "Review related study materials." });
        }

        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
        else if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(3);
        if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 3);
        clean = clean.Trim();

        try
        {
            using var doc = JsonDocument.Parse(clean);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("explanation", out var e)) explanation = e.GetString() ?? raw;
                if (doc.RootElement.TryGetProperty("remedialSteps", out var r) && r.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in r.EnumerateArray())
                    {
                        var step = s.GetString();
                        if (!string.IsNullOrWhiteSpace(step)) steps.Add(step.Trim());
                    }
                }
            }
        }
        catch
        {
            explanation = raw;
            steps.Add("Review the relevant study flashcards.");
            steps.Add("Retake practice questions for this topic.");
        }

        if (steps.Count == 0)
        {
            steps.Add("Review the core definition.");
            steps.Add("Reinforce with spaced repetition flashcards.");
        }

        return (explanation, steps);
    }

    private static TutorMiniExerciseDto ParseMiniExercise(string raw, string targetConcept)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new TutorMiniExerciseDto(
                Question: $"Explain the key role of {targetConcept}.",
                ExerciseType: "ShortAnswer",
                Options: null,
                Answer: targetConcept,
                Explanation: $"Core concept behind {targetConcept}."
            );
        }

        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
        else if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(3);
        if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 3);
        clean = clean.Trim();

        try
        {
            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;
            var q = root.TryGetProperty("question", out var qEl) ? qEl.GetString() : null;
            var t = root.TryGetProperty("exerciseType", out var tEl) ? tEl.GetString() : "MultipleChoice";
            var a = root.TryGetProperty("answer", out var aEl) ? aEl.GetString() : null;
            var e = root.TryGetProperty("explanation", out var eEl) ? eEl.GetString() : null;

            var opts = new List<string>();
            if (root.TryGetProperty("options", out var opEl) && opEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in opEl.EnumerateArray())
                {
                    var s = o.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) opts.Add(s.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(a))
            {
                return new TutorMiniExerciseDto(
                    Question: q.Trim(),
                    ExerciseType: t ?? "MultipleChoice",
                    Options: opts.Count > 0 ? opts : null,
                    Answer: a.Trim(),
                    Explanation: e ?? "Explanation based on workspace knowledge."
                );
            }
        }
        catch
        {
            // fallback
        }

        return new TutorMiniExerciseDto(
            Question: $"What is the primary function of {targetConcept}?",
            ExerciseType: "MultipleChoice",
            Options: new[] { "Option A", "Option B", "Option C", "Option D" },
            Answer: "Option A",
            Explanation: $"Fundamental concept for {targetConcept}."
        );
    }
}
