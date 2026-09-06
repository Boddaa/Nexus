using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.Features.Notes.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.AI.Services;

public class AiKnowledgeService : IAiKnowledgeService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILLMService _llmService;
    private readonly INoteService _noteService;
    private readonly AiKnowledgeOptions _options;
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<AiKnowledgeService> _logger;

    public AiKnowledgeService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ILLMService llmService,
        INoteService noteService,
        IOptions<AiKnowledgeOptions> options,
        IOptions<LlmOptions> llmOptions,
        ILogger<AiKnowledgeService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _llmService = llmService;
        _noteService = noteService;
        _options = options?.Value ?? new AiKnowledgeOptions();
        _llmOptions = llmOptions?.Value ?? new LlmOptions();
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

    private async Task<Result<(string Title, string Content, IReadOnlyList<ChatSourceDto> Sources, Guid? DocId, Guid? PageId, Guid? NoteId)>>
        ResolveSourceAsync(Guid workspaceId, string sourceType, Guid sourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
                new Error("Knowledge.InvalidSourceType", "Source type is required."));
        }

        var maxChars = _options.MaxContextCharacters;

        if (sourceType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            var document = await _context.Documents
                .AsNoTracking()
                .Where(d => d.Id == sourceId && d.WorkspaceId == workspaceId && !d.IsDeleted)
                .Select(d => new { d.Id, d.Title, d.FileName, d.ExtractedText })
                .FirstOrDefaultAsync(cancellationToken);

            if (document == null)
            {
                return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
                    new Error("Knowledge.SourceNotFound", "Document not found or inaccessible in this workspace."));
            }

            var docContextResult = await ResolveDocumentContextAsync(
                workspaceId, document.Id, document.Title, document.ExtractedText, maxChars, cancellationToken);

            if (!docContextResult.IsSuccess)
            {
                return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(docContextResult.Error);
            }

            var (content, sources) = docContextResult.Value;
            return Result.Success((document.Title, content, sources, (Guid?)document.Id, (Guid?)null, (Guid?)null));
        }

        if (sourceType.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            var page = await _context.Pages
                .AsNoTracking()
                .Where(p => p.Id == sourceId && p.WorkspaceId == workspaceId && !p.IsDeleted)
                .Select(p => new { p.Id, p.Title, p.ContentJson })
                .FirstOrDefaultAsync(cancellationToken);

            if (page == null)
            {
                return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
                    new Error("Knowledge.SourceNotFound", "Page not found or inaccessible in this workspace."));
            }

            var rawContent = CleanPageContent(page.ContentJson);
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
                    new Error("Knowledge.EmptySource", "The selected page contains no text content."));
            }

            var boundedContent = rawContent.Length > maxChars ? rawContent.Substring(0, maxChars) : rawContent;
            var source = new ChatSourceDto(
                Id: Guid.NewGuid(),
                DocumentId: null,
                DocumentChunkId: null,
                PageId: page.Id,
                NoteId: null,
                Title: page.Title,
                SourceType: "Page",
                RelevanceScore: 1.0,
                PageNumber: null,
                Snippet: boundedContent.Length > 200 ? boundedContent.Substring(0, 197) + "..." : boundedContent);

            return Result.Success((page.Title, boundedContent, (IReadOnlyList<ChatSourceDto>)new[] { source }, (Guid?)null, (Guid?)page.Id, (Guid?)null));
        }

        if (sourceType.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            var note = await _context.Notes
                .AsNoTracking()
                .Where(n => n.Id == sourceId && n.WorkspaceId == workspaceId && !n.IsDeleted)
                .Select(n => new { n.Id, n.Title, n.Content, n.PageId })
                .FirstOrDefaultAsync(cancellationToken);

            if (note == null)
            {
                return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
                    new Error("Knowledge.SourceNotFound", "Note not found or inaccessible in this workspace."));
            }

            var rawContent = note.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
                    new Error("Knowledge.EmptySource", "The selected note contains no content."));
            }

            var boundedContent = rawContent.Length > maxChars ? rawContent.Substring(0, maxChars) : rawContent;
            var source = new ChatSourceDto(
                Id: Guid.NewGuid(),
                DocumentId: null,
                DocumentChunkId: null,
                PageId: note.PageId,
                NoteId: note.Id,
                Title: note.Title,
                SourceType: "Note",
                RelevanceScore: 1.0,
                PageNumber: null,
                Snippet: boundedContent.Length > 200 ? boundedContent.Substring(0, 197) + "..." : boundedContent);

            return Result.Success((note.Title, boundedContent, (IReadOnlyList<ChatSourceDto>)new[] { source }, (Guid?)null, note.PageId, (Guid?)note.Id));
        }

        return Result.Failure<(string, string, IReadOnlyList<ChatSourceDto>, Guid?, Guid?, Guid?)>(
            new Error("Knowledge.InvalidSourceType", $"Invalid source type '{sourceType}'. Supported: Document, Page, Note."));
    }

    private sealed record ChunkSummary(Guid Id, int ChunkIndex, string Text, int? PageNumber);

    private async Task<Result<(string Content, IReadOnlyList<ChatSourceDto> Sources)>> ResolveDocumentContextAsync(
        Guid workspaceId,
        Guid documentId,
        string documentTitle,
        string? extractedText,
        int maxChars,
        CancellationToken cancellationToken)
    {
        // 1. Fast count query: determine chunk presence without materializing the chunk table into memory
        var totalChunks = await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .CountAsync(cancellationToken);

        var sb = new StringBuilder();
        var sources = new List<ChatSourceDto>();

        if (totalChunks == 0)
        {
            // Fallback to ExtractedText if unchunked
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Result.Failure<(string, IReadOnlyList<ChatSourceDto>)>(
                    new Error("Knowledge.EmptySource", "The selected document contains no readable text."));
            }

            var rawText = extractedText.Trim();
            if (rawText.Length <= maxChars)
            {
                // Small unchunked document: use complete text
                sb.Append(rawText);
            }
            else
            {
                // Large unchunked document: select 5 representative distributed sections (beginning, early-middle, middle, late-middle, ending)
                int k = 5;
                int sectionLength = Math.Max(100, (maxChars - 100) / k);
                for (int i = 0; i < k; i++)
                {
                    int start = (int)Math.Round((double)i * (rawText.Length - sectionLength) / (k - 1));
                    start = Math.Clamp(start, 0, Math.Max(0, rawText.Length - sectionLength));
                    int len = Math.Min(sectionLength, rawText.Length - start);

                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("[...]");
                        sb.AppendLine();
                    }
                    sb.Append(rawText.Substring(start, len));
                }
            }

            sources.Add(new ChatSourceDto(
                Id: Guid.NewGuid(),
                DocumentId: documentId,
                DocumentChunkId: null,
                PageId: null,
                NoteId: null,
                Title: documentTitle,
                SourceType: "Document",
                RelevanceScore: 1.0,
                PageNumber: 1,
                Snippet: rawText.Length > 200 ? rawText.Substring(0, 197) + "..." : rawText));

            return Result.Success((sb.ToString().Trim(), (IReadOnlyList<ChatSourceDto>)sources));
        }

        // 2. Chunks exist: Determine real content size via database-side aggregate before materialization
        var totalTextLength = await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .SumAsync(c => (int?)c.Text.Length, cancellationToken) ?? 0;

        if (totalTextLength <= maxChars)
        {
            // === SMALL DOCUMENT ===
            // The real total content fits within MaxContextCharacters:
            // Use the complete relevant document content without truncation.
            var allChunks = await _context.DocumentChunks
                .AsNoTracking()
                .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId && !c.IsDeleted)
                .OrderBy(c => c.StartPosition)
                .ThenBy(c => c.ChunkIndex)
                .Select(c => new ChunkSummary(c.Id, c.ChunkIndex, c.Text, c.PageNumber))
                .ToListAsync(cancellationToken);

            foreach (var chunk in allChunks)
            {
                var text = chunk.Text.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.AppendLine(text);

                sources.Add(new ChatSourceDto(
                    Id: Guid.NewGuid(),
                    DocumentId: documentId,
                    DocumentChunkId: chunk.Id,
                    PageId: null,
                    NoteId: null,
                    Title: documentTitle,
                    SourceType: "Document",
                    RelevanceScore: 1.0,
                    PageNumber: chunk.PageNumber,
                    Snippet: text.Length > 200 ? text.Substring(0, 197) + "..." : text));
            }

            var smallContent = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(smallContent))
            {
                return Result.Failure<(string, IReadOnlyList<ChatSourceDto>)>(
                    new Error("Knowledge.EmptySource", "The selected document contains no readable text."));
            }

            return Result.Success((smallContent, (IReadOnlyList<ChatSourceDto>)sources));
        }

        // === LARGE DOCUMENT ===
        // The real total content exceeds MaxContextCharacters.
        // Retrieve chunks in a strictly bounded way:
        // Do NOT load every chunk into memory.
        // Do NOT call ToListAsync() on the complete chunk table.
        // Deterministically select 5 representative chunks distributed across the document:
        // Beginning (0%), Early-Middle (25%), Middle (50%), Late-Middle (75%), Ending (100%).
        // We use positional row offsets ordered by (StartPosition, ChunkIndex) so selection is
        // robust against non-contiguous, 1-based, or gapped indexes.
        int kRepresentatives = Math.Min(5, totalChunks);
        var offsets = new List<int>();
        for (int i = 0; i < kRepresentatives; i++)
        {
            int offset = (int)Math.Round((double)i * (totalChunks - 1) / (kRepresentatives - 1));
            offsets.Add(offset);
        }

        var uniqueOffsets = offsets.Distinct().OrderBy(x => x).ToList();
        var representativeChunks = new List<ChunkSummary>();

        foreach (var offset in uniqueOffsets)
        {
            var chunk = await _context.DocumentChunks
                .AsNoTracking()
                .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId && !c.IsDeleted)
                .OrderBy(c => c.StartPosition)
                .ThenBy(c => c.ChunkIndex)
                .Skip(offset)
                .Take(1)
                .Select(c => new ChunkSummary(c.Id, c.ChunkIndex, c.Text, c.PageNumber))
                .FirstOrDefaultAsync(cancellationToken);

            if (chunk != null)
            {
                representativeChunks.Add(chunk);
            }
        }

        if (representativeChunks.Count == 0)
        {
            return Result.Failure<(string, IReadOnlyList<ChatSourceDto>)>(
                new Error("Knowledge.EmptySource", "The selected document contains no readable chunks."));
        }

        // Assemble context with proportional character budgeting:
        // Guarantees that ALL sections (beginning, early-middle, middle, late-middle, ending)
        // are represented without early chunks starving subsequent sections.
        for (int i = 0; i < representativeChunks.Count; i++)
        {
            var chunk = representativeChunks[i];
            var text = chunk.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            int remainingSlots = representativeChunks.Count - i;
            int availableChars = maxChars - sb.Length;
            if (availableChars <= 0) break;

            int maxForThisChunk = Math.Min(text.Length, availableChars / remainingSlots);
            if (maxForThisChunk <= 0) maxForThisChunk = availableChars;

            string chunkText = text.Length > maxForThisChunk
                ? text.Substring(0, Math.Max(0, maxForThisChunk - 3)) + "..."
                : text;

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
            sb.AppendLine(chunkText);

            sources.Add(new ChatSourceDto(
                Id: Guid.NewGuid(),
                DocumentId: documentId,
                DocumentChunkId: chunk.Id,
                PageId: null,
                NoteId: null,
                Title: documentTitle,
                SourceType: "Document",
                RelevanceScore: 1.0,
                PageNumber: chunk.PageNumber,
                Snippet: text.Length > 200 ? text.Substring(0, 197) + "..." : text));
        }

        var content = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure<(string, IReadOnlyList<ChatSourceDto>)>(
                new Error("Knowledge.EmptySource", "The selected document contains no readable text."));
        }

        return Result.Success((content, (IReadOnlyList<ChatSourceDto>)sources));
    }

    private static string CleanPageContent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (!raw.StartsWith("{") && !raw.StartsWith("[")) return raw.Trim();

        var cleaned = Regex.Replace(raw, @"[""{}\[\]:,]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    public async Task<Result<AiOperationResultDto>> SummarizeAsync(
        Guid workspaceId,
        AiKnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        var resolve = await ResolveSourceAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!resolve.IsSuccess) return Result.Failure<AiOperationResultDto>(resolve.Error);

        var (title, content, sources, docId, pageId, noteId) = resolve.Value;

        var systemPrompt =
            "You are NEXUS AI, an expert knowledge assistant.\n" +
            "Summarize the provided workspace knowledge accurately, concisely, and objectively.\n" +
            "Provide an executive summary followed by a 'Key Takeaways' section with bullet points.\n" +
            "Strictly base your summary on the provided context without introducing external or fabricated facts.";

        var userPrompt = BuildUserPrompt(title, request.SourceType, content, request.AdditionalInstructions);

        var llmResponse = await CallLlmSafelyAsync(systemPrompt, userPrompt, cancellationToken);
        if (!llmResponse.IsSuccess) return Result.Failure<AiOperationResultDto>(llmResponse.Error);

        var rawContent = llmResponse.Value.Content.Trim();
        var keyPoints = ExtractBullets(rawContent);

        return await PersistAndReturnAsync(
            workspaceId, userId.Value, "Summarize", rawContent,
            keyPoints.Count > 0 ? JsonSerializer.Serialize(keyPoints) : null,
            keyPoints, null, sources, docId, pageId, noteId, llmResponse.Value.Model, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> ExplainAsync(
        Guid workspaceId,
        AiKnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        var resolve = await ResolveSourceAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!resolve.IsSuccess) return Result.Failure<AiOperationResultDto>(resolve.Error);

        var (title, content, sources, docId, pageId, noteId) = resolve.Value;

        var systemPrompt =
            "You are NEXUS AI, an expert teacher and conceptual explainer.\n" +
            "Explain the concepts in the provided workspace knowledge clearly, thoroughly, and accessibly.\n" +
            "Follow any specific explanation instructions provided by the user (e.g. for a beginner, simple language, technical breakdown).\n" +
            "Ground your explanation strictly in the provided workspace material.";

        var userPrompt = BuildUserPrompt(title, request.SourceType, content, request.AdditionalInstructions);

        var llmResponse = await CallLlmSafelyAsync(systemPrompt, userPrompt, cancellationToken);
        if (!llmResponse.IsSuccess) return Result.Failure<AiOperationResultDto>(llmResponse.Error);

        var rawContent = llmResponse.Value.Content.Trim();

        return await PersistAndReturnAsync(
            workspaceId, userId.Value, "Explain", rawContent, null,
            null, null, sources, docId, pageId, noteId, llmResponse.Value.Model, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> ExtractKeyPointsAsync(
        Guid workspaceId,
        AiKnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        var resolve = await ResolveSourceAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!resolve.IsSuccess) return Result.Failure<AiOperationResultDto>(resolve.Error);

        var (title, content, sources, docId, pageId, noteId) = resolve.Value;

        var systemPrompt =
            "You are NEXUS AI.\n" +
            "Extract the essential key points from the provided workspace material.\n" +
            "Return your response ONLY as a JSON array of strings, for example:\n" +
            "[\"Core point 1\", \"Core point 2\", \"Core point 3\"]\n" +
            "Do not include extra conversational text outside the JSON array.";

        var userPrompt = BuildUserPrompt(title, request.SourceType, content, request.AdditionalInstructions);

        var llmResponse = await CallLlmSafelyAsync(systemPrompt, userPrompt, cancellationToken);
        if (!llmResponse.IsSuccess) return Result.Failure<AiOperationResultDto>(llmResponse.Error);

        var rawText = llmResponse.Value.Content.Trim();
        var keyPoints = ParseKeyPointsSafely(rawText);

        var formattedContent = string.Join("\n\n", keyPoints.Select(p => $"• {p}"));
        var structuredJson = JsonSerializer.Serialize(keyPoints);

        return await PersistAndReturnAsync(
            workspaceId, userId.Value, "KeyPoints", formattedContent, structuredJson,
            keyPoints, null, sources, docId, pageId, noteId, llmResponse.Value.Model, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> GenerateQuestionsAsync(
        Guid workspaceId,
        GenerateQuestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        var count = Math.Clamp(request.QuestionCount, 1, _options.MaxQuestionCount);
        var difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Intermediate" : request.Difficulty.Trim();

        var resolve = await ResolveSourceAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!resolve.IsSuccess) return Result.Failure<AiOperationResultDto>(resolve.Error);

        var (title, content, sources, docId, pageId, noteId) = resolve.Value;

        var systemPrompt =
            $"You are NEXUS AI, an educational test generator.\n" +
            $"Generate exactly {count} review questions based solely on the provided workspace knowledge at {difficulty} difficulty.\n" +
            $"Return your response as a JSON array of objects with the schema:\n" +
            $"[\n  {{\"question\": \"...\", \"difficulty\": \"{difficulty}\", \"answer\": \"...\"}}\n]\n" +
            $"Ensure every question and answer is grounded exclusively in the provided text.";

        var userPrompt = BuildUserPrompt(title, request.SourceType, content,
            $"Include answers: {request.IncludeAnswers}. " + (request.AdditionalInstructions ?? string.Empty));

        var llmResponse = await CallLlmSafelyAsync(systemPrompt, userPrompt, cancellationToken);
        if (!llmResponse.IsSuccess) return Result.Failure<AiOperationResultDto>(llmResponse.Error);

        var rawText = llmResponse.Value.Content.Trim();
        var questions = ParseQuestionsSafely(rawText, difficulty, sources, count);

        var sb = new StringBuilder();
        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            sb.AppendLine($"**Question {i + 1}** ({q.Difficulty}): {q.Question}");
            if (!string.IsNullOrWhiteSpace(q.Answer))
            {
                sb.AppendLine($"*Answer:* {q.Answer}");
            }
            sb.AppendLine();
        }

        var structuredJson = JsonSerializer.Serialize(questions);

        return await PersistAndReturnAsync(
            workspaceId, userId.Value, "Questions", sb.ToString().Trim(), structuredJson,
            null, questions, sources, docId, pageId, noteId, llmResponse.Value.Model, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> GenerateStudyMaterialAsync(
        Guid workspaceId,
        GenerateStudyMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        var resolve = await ResolveSourceAsync(workspaceId, request.SourceType, request.SourceId, cancellationToken);
        if (!resolve.IsSuccess) return Result.Failure<AiOperationResultDto>(resolve.Error);

        var (title, content, sources, docId, pageId, noteId) = resolve.Value;

        var systemPrompt =
            "You are NEXUS AI, an educational learning designer.\n" +
            "Generate structured study material from the provided knowledge.\n" +
            "Return your response formatted as a JSON object with three keys:\n" +
            "{\n" +
            "  \"studySummary\": \"Comprehensive conceptual summary of the material...\",\n" +
            "  \"keyPoints\": [\"Point 1\", \"Point 2\", \"Point 3\"],\n" +
            "  \"questions\": [\n" +
            "    {\"question\": \"...\", \"difficulty\": \"Intermediate\", \"answer\": \"...\"}\n" +
            "  ]\n" +
            "}\n" +
            "Base everything strictly on the provided workspace context.";

        var userPrompt = BuildUserPrompt(title, request.SourceType, content, request.AdditionalInstructions);

        var llmResponse = await CallLlmSafelyAsync(systemPrompt, userPrompt, cancellationToken);
        if (!llmResponse.IsSuccess) return Result.Failure<AiOperationResultDto>(llmResponse.Error);

        var rawText = llmResponse.Value.Content.Trim();
        var (studySummary, keyPoints, questions) = ParseStudyMaterialSafely(rawText, sources);

        var structuredObj = new
        {
            studySummary,
            keyPoints,
            questions
        };
        var structuredJson = JsonSerializer.Serialize(structuredObj);

        return await PersistAndReturnAsync(
            workspaceId, userId.Value, "StudyMaterial", studySummary, structuredJson,
            keyPoints, questions, sources, docId, pageId, noteId, llmResponse.Value.Model, cancellationToken);
    }

    public async Task<Result<NoteDto>> SaveAsNoteAsync(
        Guid workspaceId,
        SaveAiOutputAsNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<NoteDto>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<NoteDto>(Error.Unauthorized);

        if (string.IsNullOrWhiteSpace(request?.Title))
            return Result.Failure<NoteDto>(new Error("Note.EmptyTitle", "Note title cannot be empty."));

        // Load generation and ensure ownership and workspace match
        var generation = await _context.AiGenerations
            .Include(g => g.Sources)
            .FirstOrDefaultAsync(g => g.Id == request.AiGenerationId && g.WorkspaceId == workspaceId && g.UserId == userId.Value && !g.IsDeleted, cancellationToken);

        if (generation == null)
        {
            return Result.Failure<NoteDto>(new Error("AiGeneration.NotFound", "AI generation result not found or access denied."));
        }

        // Validate destination Page exists within this workspace
        var pageExists = await _context.Pages
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PageId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

        if (!pageExists)
        {
            return Result.Failure<NoteDto>(new Error("Note.PageNotFound", "Destination page not found in this workspace."));
        }

        // Build note content combining generated text and structured items
        var noteBody = new StringBuilder();
        noteBody.AppendLine(generation.Content);

        if (!string.IsNullOrWhiteSpace(generation.StructuredContentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(generation.StructuredContentJson);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    // Could be KeyPoints or Questions
                    noteBody.AppendLine();
                    noteBody.AppendLine("---");
                    noteBody.AppendLine("### Key Details");
                    foreach (var elem in root.EnumerateArray())
                    {
                        if (elem.ValueKind == JsonValueKind.String)
                        {
                            noteBody.AppendLine($"- {elem.GetString()}");
                        }
                        else if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("question", out var qProp))
                        {
                            var qText = qProp.GetString();
                            var aText = elem.TryGetProperty("answer", out var aProp) ? aProp.GetString() : null;
                            noteBody.AppendLine($"**Q:** {qText}");
                            if (!string.IsNullOrWhiteSpace(aText))
                            {
                                noteBody.AppendLine($"**A:** {aText}");
                            }
                            noteBody.AppendLine();
                        }
                    }
                }
            }
            catch
            {
                // If secondary parsing fails, generation.Content is already safely included
            }
        }

        noteBody.AppendLine();
        noteBody.AppendLine("---");
        noteBody.AppendLine($"*Generated by NEXUS AI ({generation.Operation}) on {generation.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC*");

        // Save note via existing INoteService
        var createRequest = new CreateNoteRequest(
            Title: request.Title.Trim(),
            Content: noteBody.ToString().Trim(),
            ContentType: "markdown",
            IsPinned: false,
            PageId: request.PageId,
            Tags: new List<string> { "AI-Generated", generation.Operation });

        return await _noteService.CreateNoteAsync(workspaceId, createRequest, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<AiGenerationSummaryDto>>> GetGenerationsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<IReadOnlyList<AiGenerationSummaryDto>>(Error.Unauthorized);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
            return Result.Failure<IReadOnlyList<AiGenerationSummaryDto>>(Error.Unauthorized);

        var generations = await _context.AiGenerations
            .AsNoTracking()
            .Where(g => g.WorkspaceId == workspaceId && g.UserId == userId.Value && !g.IsDeleted)
            .OrderByDescending(g => g.CreatedAtUtc)
            .Select(g => new AiGenerationSummaryDto(
                g.Id,
                g.Operation,
                g.Content.Length > 120 ? g.Content.Substring(0, 117) + "..." : g.Content,
                g.SourceDocumentId != null ? "Document" : (g.SourcePageId != null ? "Page" : (g.SourceNoteId != null ? "Note" : null)),
                g.SourceDocumentId ?? g.SourcePageId ?? g.SourceNoteId,
                g.Model,
                g.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AiGenerationSummaryDto>>(generations);
    }

    public async Task<Result<AiOperationResultDto>> GetGenerationAsync(
        Guid workspaceId,
        Guid generationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<AiOperationResultDto>(Error.Unauthorized);

        var generation = await _context.AiGenerations
            .Include(g => g.Sources)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == generationId && g.WorkspaceId == workspaceId && g.UserId == userId.Value && !g.IsDeleted, cancellationToken);

        if (generation == null)
        {
            return Result.Failure<AiOperationResultDto>(new Error("AiGeneration.NotFound", "AI generation not found."));
        }

        IReadOnlyList<string>? keyPoints = null;
        IReadOnlyList<GeneratedQuestionDto>? questions = null;

        if (!string.IsNullOrWhiteSpace(generation.StructuredContentJson))
        {
            try
            {
                if (generation.Operation == "KeyPoints")
                {
                    keyPoints = JsonSerializer.Deserialize<List<string>>(generation.StructuredContentJson);
                }
                else if (generation.Operation == "Questions")
                {
                    questions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(generation.StructuredContentJson);
                }
                else if (generation.Operation == "StudyMaterial")
                {
                    using var doc = JsonDocument.Parse(generation.StructuredContentJson);
                    if (doc.RootElement.TryGetProperty("keyPoints", out var kpProp))
                    {
                        keyPoints = JsonSerializer.Deserialize<List<string>>(kpProp.GetRawText());
                    }
                    if (doc.RootElement.TryGetProperty("questions", out var qProp))
                    {
                        questions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(qProp.GetRawText());
                    }
                }
            }
            catch
            {
                // Ignore deserialize failure on legacy/scratch JSON
            }
        }

        var sources = generation.Sources.Select(s => new ChatSourceDto(
            s.Id,
            s.DocumentId,
            s.DocumentChunkId,
            s.PageId,
            s.NoteId,
            s.Title ?? "Source",
            s.DocumentId != null ? "Document" : (s.PageId != null ? "Page" : "Note"),
            s.RelevanceScore,
            s.PageNumber,
            s.Snippet)).ToList();

        return Result.Success(new AiOperationResultDto(
            generation.Id,
            generation.Operation,
            generation.Content,
            keyPoints,
            questions,
            sources,
            generation.Model,
            generation.CreatedAtUtc));
    }

    public async Task<Result<bool>> DeleteGenerationAsync(
        Guid workspaceId,
        Guid generationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Result.Failure<bool>(Error.Unauthorized);

        var generation = await _context.AiGenerations
            .FirstOrDefaultAsync(g => g.Id == generationId && g.WorkspaceId == workspaceId && g.UserId == userId.Value && !g.IsDeleted, cancellationToken);

        if (generation == null)
        {
            return Result.Failure<bool>(new Error("AiGeneration.NotFound", "AI generation not found."));
        }

        generation.IsDeleted = true;
        generation.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }

    #region Helper Methods

    private string BuildUserPrompt(string title, string type, string content, string? instructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source Knowledge [{type}]: {title}");
        sb.AppendLine("---");
        sb.AppendLine(content);
        sb.AppendLine("---");

        if (!string.IsNullOrWhiteSpace(instructions))
        {
            var cleanInstructions = instructions.Trim();
            if (cleanInstructions.Length > _options.MaxInstructionCharacters)
            {
                cleanInstructions = cleanInstructions.Substring(0, _options.MaxInstructionCharacters);
            }
            sb.AppendLine();
            sb.AppendLine($"User Instructions: {cleanInstructions}");
        }

        return sb.ToString();
    }

    private async Task<Result<LLMResponse>> CallLlmSafelyAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var request = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", userPrompt) },
            SystemPrompt: systemPrompt,
            Temperature: _llmOptions.Temperature,
            MaxTokens: _llmOptions.MaxTokens,
            Model: _llmOptions.Model);

        try
        {
            var response = await _llmService.ChatAsync(request, cancellationToken);
            return Result.Success(response);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<LLMResponse>(new Error("AI.Cancelled", "AI operation was cancelled."));
        }
        catch (LlmConfigurationException ex)
        {
            _logger.LogError(ex, "LLM configuration error in AiKnowledgeService");
            return Result.Failure<LLMResponse>(new Error("AI.NotConfigured", ex.Message));
        }
        catch (LlmException ex)
        {
            _logger.LogError(ex, "LLM provider error in AiKnowledgeService");
            return Result.Failure<LLMResponse>(new Error("AI.ProviderError", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AiKnowledgeService LLM call");
            return Result.Failure<LLMResponse>(new Error("AI.ExecutionError", "Failed to execute AI request."));
        }
    }

    private async Task<Result<AiOperationResultDto>> PersistAndReturnAsync(
        Guid workspaceId,
        Guid userId,
        string operation,
        string content,
        string? structuredJson,
        IReadOnlyList<string>? keyPoints,
        IReadOnlyList<GeneratedQuestionDto>? questions,
        IReadOnlyList<ChatSourceDto> sources,
        Guid? docId,
        Guid? pageId,
        Guid? noteId,
        string? model,
        CancellationToken cancellationToken)
    {
        var generation = new AiGeneration
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            SourceDocumentId = docId,
            SourcePageId = pageId,
            SourceNoteId = noteId,
            Operation = operation,
            Content = content,
            StructuredContentJson = structuredJson,
            Model = string.IsNullOrWhiteSpace(model) ? _llmOptions.Model : model
        };

        _context.AiGenerations.Add(generation);

        foreach (var s in sources)
        {
            generation.Sources.Add(new AiGenerationSource
            {
                AiGenerationId = generation.Id,
                DocumentId = s.DocumentId,
                DocumentChunkId = s.DocumentChunkId,
                PageId = s.PageId,
                NoteId = s.NoteId,
                Title = s.Title,
                Snippet = s.Snippet,
                RelevanceScore = s.RelevanceScore,
                PageNumber = s.PageNumber
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new AiOperationResultDto(
            generation.Id,
            generation.Operation,
            generation.Content,
            keyPoints,
            questions,
            sources,
            generation.Model,
            generation.CreatedAtUtc));
    }

    private static IReadOnlyList<string> ParseKeyPointsSafely(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return Array.Empty<string>();

        // 1. Try JSON array parsing
        try
        {
            var jsonText = ExtractJsonBlock(rawText);
            var parsed = JsonSerializer.Deserialize<List<string>>(jsonText);
            if (parsed != null && parsed.Count > 0)
            {
                return parsed.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
            }
        }
        catch
        {
            // Fallback to text parsing
        }

        // 2. Fallback: extract bullet points or numbered lines
        var bullets = ExtractBullets(rawText);
        if (bullets.Count > 0) return bullets;

        // 3. Fallback: split by sentence or paragraphs
        return rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 10)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<GeneratedQuestionDto> ParseQuestionsSafely(
        string rawText, string defaultDifficulty, IReadOnlyList<ChatSourceDto> sources, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return Array.Empty<GeneratedQuestionDto>();

        // 1. Try JSON array parsing
        try
        {
            var jsonText = ExtractJsonBlock(rawText);
            using var doc = JsonDocument.Parse(jsonText);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<GeneratedQuestionDto>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("question", out var qProp))
                    {
                        var qStr = qProp.GetString();
                        if (string.IsNullOrWhiteSpace(qStr)) continue;

                        var diff = item.TryGetProperty("difficulty", out var dProp) ? dProp.GetString() : defaultDifficulty;
                        var ans = item.TryGetProperty("answer", out var aProp) ? aProp.GetString() : null;

                        list.Add(new GeneratedQuestionDto(
                            Id: Guid.NewGuid(),
                            Question: qStr.Trim(),
                            Difficulty: string.IsNullOrWhiteSpace(diff) ? defaultDifficulty : diff.Trim(),
                            Answer: ans?.Trim(),
                            Sources: sources));

                        if (list.Count >= maxCount) break;
                    }
                }

                if (list.Count > 0) return list;
            }
        }
        catch
        {
            // Fallback to text parsing
        }

        // 2. Fallback: line/regex extraction
        var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fallbackList = new List<GeneratedQuestionDto>();

        string? currentQuestion = null;
        string? currentAnswer = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("Q:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Question", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(line, @"^\d+[\.\)]\s+"))
            {
                if (currentQuestion != null)
                {
                    fallbackList.Add(new GeneratedQuestionDto(Guid.NewGuid(), currentQuestion, defaultDifficulty, currentAnswer, sources));
                    if (fallbackList.Count >= maxCount) break;
                }

                currentQuestion = Regex.Replace(line, @"^(Q:|Question\s*\d*:?|\d+[\.\)])\s*", "", RegexOptions.IgnoreCase).Trim();
                currentAnswer = null;
            }
            else if (line.StartsWith("A:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("Answer:", StringComparison.OrdinalIgnoreCase))
            {
                currentAnswer = Regex.Replace(line, @"^(A:|Answer:)\s*", "", RegexOptions.IgnoreCase).Trim();
            }
        }

        if (currentQuestion != null && fallbackList.Count < maxCount)
        {
            fallbackList.Add(new GeneratedQuestionDto(Guid.NewGuid(), currentQuestion, defaultDifficulty, currentAnswer, sources));
        }

        return fallbackList;
    }

    private static (string Summary, IReadOnlyList<string> KeyPoints, IReadOnlyList<GeneratedQuestionDto> Questions)
        ParseStudyMaterialSafely(string rawText, IReadOnlyList<ChatSourceDto> sources)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return (string.Empty, Array.Empty<string>(), Array.Empty<GeneratedQuestionDto>());

        try
        {
            var jsonText = ExtractJsonBlock(rawText);
            using var doc = JsonDocument.Parse(jsonText);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var summary = doc.RootElement.TryGetProperty("studySummary", out var sProp) ? sProp.GetString() ?? string.Empty : string.Empty;
                var keyPoints = new List<string>();
                if (doc.RootElement.TryGetProperty("keyPoints", out var kpProp) && kpProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var k in kpProp.EnumerateArray())
                    {
                        var s = k.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) keyPoints.Add(s.Trim());
                    }
                }

                var questions = new List<GeneratedQuestionDto>();
                if (doc.RootElement.TryGetProperty("questions", out var qProp) && qProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var qElem in qProp.EnumerateArray())
                    {
                        if (qElem.TryGetProperty("question", out var questionText))
                        {
                            var q = questionText.GetString();
                            var diff = qElem.TryGetProperty("difficulty", out var dProp) ? dProp.GetString() : "Intermediate";
                            var a = qElem.TryGetProperty("answer", out var aProp) ? aProp.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(q))
                            {
                                questions.Add(new GeneratedQuestionDto(Guid.NewGuid(), q.Trim(), diff ?? "Intermediate", a?.Trim(), sources));
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(summary) || keyPoints.Count > 0 || questions.Count > 0)
                {
                    return (summary, keyPoints, questions);
                }
            }
        }
        catch
        {
            // Fallback to text parsing
        }

        // Fallback: full text as summary and bullet extraction
        var extractedBullets = ExtractBullets(rawText);
        return (rawText, extractedBullets, Array.Empty<GeneratedQuestionDto>());
    }

    private static string ExtractJsonBlock(string rawText)
    {
        var match = Regex.Match(rawText, @"```(?:json)?\s*([\[{].*?[\]}])\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        var firstBracket = rawText.IndexOfAny(new[] { '[', '{' });
        var lastBracket = rawText.LastIndexOfAny(new[] { ']', '}' });
        if (firstBracket >= 0 && lastBracket > firstBracket)
        {
            return rawText.Substring(firstBracket, lastBracket - firstBracket + 1).Trim();
        }

        return rawText;
    }

    private static IReadOnlyList<string> ExtractBullets(string text)
    {
        var bullets = new List<string>();
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^[-*•]\s+"))
            {
                var clean = Regex.Replace(line, @"^[-*•]\s+", "").Trim();
                if (clean.Length > 5) bullets.Add(clean);
            }
            else if (Regex.IsMatch(line, @"^\d+[\.\)]\s+"))
            {
                var clean = Regex.Replace(line, @"^\d+[\.\)]\s+", "").Trim();
                if (clean.Length > 5) bullets.Add(clean);
            }
        }

        return bullets;
    }

    #endregion
}
