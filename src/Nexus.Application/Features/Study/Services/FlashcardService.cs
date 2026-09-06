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

public class FlashcardService : IFlashcardService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISpacedRepetitionService _spacedRepetitionService;
    private readonly ILLMService? _llmService;
    private readonly StudyOptions _options;
    private readonly ILogger<FlashcardService> _logger;

    public FlashcardService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ISpacedRepetitionService spacedRepetitionService,
        IOptions<StudyOptions> options,
        ILogger<FlashcardService> logger,
        ILLMService? llmService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _spacedRepetitionService = spacedRepetitionService;
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

    public async Task<Result<IReadOnlyList<FlashcardDto>>> GetFlashcardsAsync(
        Guid workspaceId,
        Guid? topicId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var query = _context.Flashcards
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted);

        if (topicId.HasValue)
        {
            query = query.Where(f => f.StudyTopicId == topicId.Value);
        }

        var list = await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => MapToDto(f))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FlashcardDto>>(list);
    }

    public async Task<Result<IReadOnlyList<FlashcardDto>>> GetDueFlashcardsAsync(
        Guid workspaceId,
        Guid? topicId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var now = DateTime.UtcNow;
        var query = _context.Flashcards
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted && f.NextReviewDateUtc <= now);

        if (topicId.HasValue)
        {
            query = query.Where(f => f.StudyTopicId == topicId.Value);
        }

        var list = await query
            .OrderBy(f => f.NextReviewDateUtc)
            .Select(f => MapToDto(f))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FlashcardDto>>(list);
    }

    public async Task<Result<FlashcardDto>> GetFlashcardByIdAsync(
        Guid workspaceId,
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<FlashcardDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<FlashcardDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var card = await _context.Flashcards
            .AsNoTracking()
            .Where(f => f.Id == cardId && f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted)
            .Select(f => MapToDto(f))
            .FirstOrDefaultAsync(cancellationToken);

        if (card == null)
        {
            return Result.Failure<FlashcardDto>(new Error("Flashcard.NotFound", "Flashcard not found."));
        }

        return Result.Success(card);
    }

    public async Task<Result<FlashcardDto>> CreateFlashcardAsync(
        Guid workspaceId,
        Guid? topicId,
        CreateFlashcardRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<FlashcardDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<FlashcardDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request?.FrontText) || string.IsNullOrWhiteSpace(request?.BackText))
        {
            return Result.Failure<FlashcardDto>(new Error("Flashcard.Validation", "Both front and back text are required."));
        }

        if (topicId.HasValue)
        {
            var topicExists = await _context.StudyTopics
                .AsNoTracking()
                .AnyAsync(t => t.Id == topicId.Value && t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted, cancellationToken);
            if (!topicExists)
            {
                return Result.Failure<FlashcardDto>(new Error("Flashcard.TopicNotFound", "Study topic not found in this workspace."));
            }
        }

        var now = DateTime.UtcNow;
        var card = new Flashcard
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            StudyTopicId = topicId,
            FrontText = request.FrontText.Trim(),
            BackText = request.BackText.Trim(),
            Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
            EaseFactor = 2.5,
            IntervalDays = 1,
            Repetitions = 0,
            NextReviewDateUtc = now,
            State = FlashcardState.New,
            SourceDocumentId = request.SourceDocumentId,
            SourcePageId = request.SourcePageId,
            SourceNoteId = request.SourceNoteId,
            ReviewCount = 0,
            CorrectCount = 0,
            WrongCount = 0
        };

        _context.Flashcards.Add(card);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(card));
    }

    public async Task<Result<FlashcardDto>> ReviewFlashcardAsync(
        Guid workspaceId,
        Guid cardId,
        ReviewFlashcardRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<FlashcardDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<FlashcardDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var card = await _context.Flashcards
            .FirstOrDefaultAsync(f => f.Id == cardId && f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted, cancellationToken);

        if (card == null)
        {
            return Result.Failure<FlashcardDto>(new Error("Flashcard.NotFound", "Flashcard not found."));
        }

        var now = DateTime.UtcNow;
        var result = _spacedRepetitionService.CalculateNextReview(card, request.Rating, now);

        card.Repetitions = result.Repetitions;
        card.IntervalDays = result.IntervalDays;
        card.EaseFactor = result.EaseFactor;
        card.NextReviewDateUtc = result.NextReviewDateUtc;
        card.State = result.State;
        card.LastReviewedAtUtc = now;
        card.ReviewCount++;

        if (request.Rating == ReviewRating.Again)
        {
            card.WrongCount++;
        }
        else
        {
            card.CorrectCount++;
        }

        card.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(card));
    }

    public async Task<Result<IReadOnlyList<FlashcardDto>>> GenerateFlashcardsAsync(
        Guid workspaceId,
        Guid? topicId,
        GenerateFlashcardsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (topicId.HasValue)
        {
            var topicExists = await _context.StudyTopics
                .AsNoTracking()
                .AnyAsync(t => t.Id == topicId.Value && t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted, cancellationToken);
            if (!topicExists)
            {
                return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Flashcard.TopicNotFound", "Study topic not found."));
            }
        }

        if (_llmService == null)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("LLM.NotConfigured", "LLM service is not available for generation."));
        }

        var count = Math.Clamp(request.Count, 1, _options.MaxFlashcardGenerationCount);
        var sourceResolution = await ResolveSourceContentAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!sourceResolution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(sourceResolution.Error);
        }

        var (sourceTitle, sourceContent, docId, pageId, noteId) = sourceResolution.Value;

        var systemPrompt = "You are an expert tutor and instructional designer. Generate high-yield study flashcards based strictly on the provided material. " +
                           "Format your response as a valid JSON array of objects with keys: \"front\" (question/prompt), \"back\" (answer/key explanation), \"difficulty\" (\"Easy\", \"Medium\", \"Hard\"). " +
                           "Do not wrap in anything other than the JSON array.";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Source: {sourceTitle}");
        userPrompt.AppendLine($"Target count: {count}");
        userPrompt.AppendLine($"Target difficulty: {request.Difficulty}");
        if (!string.IsNullOrWhiteSpace(request.AdditionalInstructions))
        {
            userPrompt.AppendLine($"Additional instructions: {request.AdditionalInstructions.Trim()}");
        }
        userPrompt.AppendLine();
        userPrompt.AppendLine("Reference Content:");
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
            _logger.LogError(ex, "Failed to invoke LLM for flashcard generation in workspace {WorkspaceId}", workspaceId);
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("LLM.InvocationError", "Failed to generate flashcards from AI service."));
        }

        var parsedCards = ParseGeneratedFlashcards(llmResponse.Content);
        if (parsedCards.Count == 0)
        {
            // Fallback: generate a basic card from source title
            parsedCards.Add(new ParsedCard(
                Front: $"Key concept from {sourceTitle}",
                Back: sourceContent.Length > 200 ? sourceContent.Substring(0, 197) + "..." : sourceContent,
                Difficulty: request.Difficulty
            ));
        }

        var now = DateTime.UtcNow;
        var createdEntities = new List<Flashcard>();

        foreach (var item in parsedCards.Take(count))
        {
            var card = new Flashcard
            {
                WorkspaceId = workspaceId,
                UserId = userId.Value,
                StudyTopicId = topicId,
                FrontText = item.Front.Trim(),
                BackText = item.Back.Trim(),
                Difficulty = string.IsNullOrWhiteSpace(item.Difficulty) ? request.Difficulty : item.Difficulty.Trim(),
                EaseFactor = 2.5,
                IntervalDays = 1,
                Repetitions = 0,
                NextReviewDateUtc = now,
                State = FlashcardState.New,
                SourceDocumentId = docId,
                SourcePageId = pageId,
                SourceNoteId = noteId,
                ReviewCount = 0,
                CorrectCount = 0,
                WrongCount = 0
            };

            _context.Flashcards.Add(card);
            createdEntities.Add(card);
        }

        var aiGen = new AiGeneration
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            SourceDocumentId = docId,
            SourcePageId = pageId,
            SourceNoteId = noteId,
            Operation = "GenerateFlashcards",
            Content = $"Generated {createdEntities.Count} flashcards from {sourceTitle}.",
            StructuredContentJson = llmResponse.Content,
            Model = "llm"
        };
        _context.AiGenerations.Add(aiGen);

        foreach (var card in createdEntities)
        {
            card.AiGenerationId = aiGen.Id;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dtos = createdEntities.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<FlashcardDto>>(dtos);
    }

    public async Task<Result<bool>> DeleteFlashcardAsync(
        Guid workspaceId,
        Guid cardId,
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

        var card = await _context.Flashcards
            .FirstOrDefaultAsync(f => f.Id == cardId && f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted, cancellationToken);

        if (card == null)
        {
            return Result.Failure<bool>(new Error("Flashcard.NotFound", "Flashcard not found."));
        }

        card.IsDeleted = true;
        card.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    private static FlashcardDto MapToDto(Flashcard f) => new(
        Id: f.Id,
        WorkspaceId: f.WorkspaceId,
        UserId: f.UserId,
        StudyTopicId: f.StudyTopicId,
        ConceptId: f.ConceptId,
        FrontText: f.FrontText,
        BackText: f.BackText,
        EaseFactor: f.EaseFactor,
        Repetitions: f.Repetitions,
        IntervalDays: f.IntervalDays,
        NextReviewAtUtc: f.NextReviewDateUtc,
        ReviewCount: f.ReviewCount,
        CorrectCount: f.CorrectCount,
        WrongCount: f.WrongCount,
        LastReviewedAtUtc: f.LastReviewedAtUtc,
        Difficulty: f.Difficulty,
        State: f.State,
        SourceDocumentId: f.SourceDocumentId,
        SourceDocumentChunkId: f.SourceDocumentChunkId,
        SourcePageId: f.SourcePageId,
        SourceNoteId: f.SourceNoteId,
        AiGenerationId: f.AiGenerationId,
        CreatedAtUtc: f.CreatedAtUtc
    );

    private async Task<Result<(string Title, string Content)>> BuildDocumentContextAsync(
        Guid workspaceId,
        Guid documentId,
        int maxChars,
        CancellationToken ct)
    {
        var doc = await _context.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId && d.WorkspaceId == workspaceId && !d.IsDeleted)
            .Select(d => new { d.Id, d.Title, d.ExtractedText })
            .FirstOrDefaultAsync(ct);

        if (doc == null)
        {
            return Result.Failure<(string, string)>(new Error("Source.NotFound", "Document not found."));
        }

        var chunkQuery = _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == doc.Id && c.WorkspaceId == workspaceId && !c.IsDeleted);

        var totalChunks = await chunkQuery.CountAsync(ct);
        if (totalChunks == 0)
        {
            var rawText = doc.ExtractedText?.Trim() ?? string.Empty;
            if (rawText.Length > maxChars) rawText = rawText.Substring(0, maxChars);
            return Result.Success((doc.Title, rawText));
        }

        var totalTextLength = await chunkQuery.SumAsync(c => (int?)c.Text.Length, ct) ?? 0;
        if (totalTextLength <= maxChars)
        {
            var allChunks = await chunkQuery
                .OrderBy(c => c.StartPosition)
                .ThenBy(c => c.ChunkIndex)
                .Select(c => c.Text)
                .ToListAsync(ct);

            var fullContent = string.Join("\n\n", allChunks.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
            return Result.Success((doc.Title, fullContent));
        }

        // Large document: select up to 5 representative chunks distributed across the document
        int kRepresentatives = Math.Min(5, totalChunks);
        var offsets = new List<int>();
        for (int i = 0; i < kRepresentatives; i++)
        {
            int offset = (int)Math.Round((double)i * (totalChunks - 1) / (kRepresentatives - 1));
            offsets.Add(offset);
        }

        var uniqueOffsets = offsets.Distinct().OrderBy(x => x).ToList();
        var representativeTexts = new List<string>();

        foreach (var offset in uniqueOffsets)
        {
            var chunkText = await chunkQuery
                .OrderBy(c => c.StartPosition)
                .ThenBy(c => c.ChunkIndex)
                .Skip(offset)
                .Take(1)
                .Select(c => c.Text)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                representativeTexts.Add(chunkText.Trim());
            }
        }

        var sb = new StringBuilder();
        for (int i = 0; i < representativeTexts.Count; i++)
        {
            var text = representativeTexts[i];
            int remainingSlots = representativeTexts.Count - i;
            int availableChars = maxChars - sb.Length;
            if (availableChars <= 0) break;

            int maxForThisChunk = Math.Min(text.Length, availableChars / remainingSlots);
            if (maxForThisChunk <= 0) maxForThisChunk = availableChars;

            string snippet = text.Length > maxForThisChunk
                ? text.Substring(0, Math.Max(0, maxForThisChunk - 3)) + "..."
                : text;

            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(snippet);
        }

        return Result.Success((doc.Title, sb.ToString()));
    }

    private async Task<Result<(string Title, string Content, Guid? DocId, Guid? PageId, Guid? NoteId)>>
        ResolveSourceContentAsync(Guid workspaceId, string sourceType, Guid sourceId, CancellationToken ct)
    {
        var maxChars = _options.MaxContextCharacters;

        if (sourceType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            var docRes = await BuildDocumentContextAsync(workspaceId, sourceId, maxChars, ct);
            if (!docRes.IsSuccess) return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(docRes.Error);
            return Result.Success((docRes.Value.Title, docRes.Value.Content, (Guid?)sourceId, (Guid?)null, (Guid?)null));
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
            var currentUserId = _currentUserService.UserId;
            var topic = await _context.StudyTopics
                .AsNoTracking()
                .Where(t => t.Id == sourceId && t.WorkspaceId == workspaceId && (!currentUserId.HasValue || t.UserId == currentUserId.Value) && !t.IsDeleted)
                .Select(t => new { t.Id, t.Title, t.Description, t.SourceDocumentId, t.SourcePageId, t.SourceNoteId })
                .FirstOrDefaultAsync(ct);

            if (topic == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Study topic not found."));
            }

            // If topic points to an underlying document/page/note, resolve it
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

    private sealed record ParsedCard(string Front, string Back, string Difficulty);

    private static List<ParsedCard> ParseGeneratedFlashcards(string raw)
    {
        var results = new List<ParsedCard>();
        if (string.IsNullOrWhiteSpace(raw)) return results;

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
                    var front = el.TryGetProperty("front", out var f) ? f.GetString() : null;
                    if (string.IsNullOrWhiteSpace(front) && el.TryGetProperty("question", out var q)) front = q.GetString();

                    var back = el.TryGetProperty("back", out var b) ? b.GetString() : null;
                    if (string.IsNullOrWhiteSpace(back) && el.TryGetProperty("answer", out var a)) back = a.GetString();

                    var diff = el.TryGetProperty("difficulty", out var d) ? d.GetString() : "Medium";

                    if (!string.IsNullOrWhiteSpace(front) && !string.IsNullOrWhiteSpace(back))
                    {
                        results.Add(new ParsedCard(front.Trim(), back.Trim(), diff?.Trim() ?? "Medium"));
                    }
                }
            }
        }
        catch
        {
            // Defensive regex fallback if json is malformed
            var matches = Regex.Matches(raw, @"\""front\""\s*:\s*\""(?<front>[^\""]+)\""[,\s]*\""back\""\s*:\s*\""(?<back>[^\""]+)\""", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var f = m.Groups["front"].Value;
                var b = m.Groups["back"].Value;
                if (!string.IsNullOrWhiteSpace(f) && !string.IsNullOrWhiteSpace(b))
                {
                    results.Add(new ParsedCard(f.Trim(), b.Trim(), "Medium"));
                }
            }
        }

        return results;
    }
}
