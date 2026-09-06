using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.Features.Search.Services;
using Nexus.Domain.Common;
using SearchRequest = Nexus.Application.DTOs.Search.SearchRequest;
using SearchResultDto = Nexus.Application.DTOs.Search.SearchResultDto;

namespace Nexus.Application.Features.AI.Services;

public class RagService : IRagService
{
    private readonly ISearchService _searchService;
    private readonly IContextBuilder _contextBuilder;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ICitationValidator _citationValidator;
    private readonly ILLMService _llmService;
    private readonly RagOptions _ragOptions;
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<RagService> _logger;

    public RagService(
        ISearchService searchService,
        IContextBuilder contextBuilder,
        IPromptBuilder promptBuilder,
        ICitationValidator citationValidator,
        ILLMService llmService,
        IOptions<RagOptions> ragOptions,
        IOptions<LlmOptions> llmOptions,
        ILogger<RagService> logger)
    {
        _searchService = searchService;
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _citationValidator = citationValidator;
        _llmService = llmService;
        _ragOptions = ragOptions?.Value ?? new RagOptions();
        _llmOptions = llmOptions?.Value ?? new LlmOptions();
        _logger = logger;
    }

    public async Task<Result<RagAnswerResult>> AnswerQuestionAsync(
        Guid workspaceId,
        string question,
        IReadOnlyList<LLMChatMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return Result.Failure<RagAnswerResult>(new Error("Rag.EmptyQuestion", "Question cannot be empty."));
        }

        // 1. Retrieve relevant knowledge via SearchService
        var searchRequest = new SearchRequest(
            Query: question.Trim(),
            Mode: _ragOptions.RetrievalMode,
            TopK: _ragOptions.TopK,
            Page: 1,
            PageSize: _ragOptions.TopK);

        var searchResult = await _searchService.SearchAsync(workspaceId, searchRequest, cancellationToken);
        if (!searchResult.IsSuccess)
        {
            _logger.LogWarning("Search retrieval failed during RAG execution: {Error}", searchResult.Error.Description);
            return Result.Failure<RagAnswerResult>(searchResult.Error);
        }

        // 2. Filter & Map to ChatSourceDto using normalized relevance score
        var candidateItems = searchResult.Value.Items;
        var sources = new List<ChatSourceDto>();

        foreach (var item in candidateItems)
        {
            // Deterministic normalized relevance score in [0.0, 1.0] from SearchResultDto
            var normalizedScore = item.NormalizedScore ?? (item.SimilarityScore.HasValue
                ? Math.Clamp(item.SimilarityScore.Value, 0.0, 1.0)
                : Math.Clamp(item.Score / 100.0, 0.0, 1.0));

            // Apply minimum relevance filter on the normalized [0.0, 1.0] scale
            if (normalizedScore < _ragOptions.MinimumRelevanceScore)
            {
                continue;
            }

            sources.Add(new ChatSourceDto(
                Id: Guid.NewGuid(),
                DocumentId: item.Type.Equals("Document", StringComparison.OrdinalIgnoreCase) ? item.Id : null,
                DocumentChunkId: item.ChunkId,
                PageId: item.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) ? item.Id : null,
                NoteId: item.Type.Equals("Note", StringComparison.OrdinalIgnoreCase) ? item.Id : null,
                Title: item.Title,
                SourceType: item.Type,
                RelevanceScore: Math.Round(normalizedScore, 4),
                PageNumber: item.PageNumber,
                Snippet: item.Snippet));

            if (sources.Count >= _ragOptions.MaxSources)
            {
                break;
            }
        }

        // 3. Hallucination Protection: if no relevant sources found
        if (sources.Count == 0)
        {
            _logger.LogInformation("No relevant sources found above threshold for query: {Query}", question);
            return Result.Success(new RagAnswerResult(
                Answer: "I could not find any relevant information in this workspace to answer your question. Please ensure relevant documents, pages, or notes are uploaded or added to the workspace.",
                Sources: Array.Empty<ChatSourceDto>(),
                PromptTokens: 0,
                CompletionTokens: 0,
                TotalTokens: 0));
        }

        // 4. Build bounded context
        var context = _contextBuilder.BuildContext(sources, _ragOptions.MaxContextCharacters);

        // 5. Build prompt
        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = _promptBuilder.BuildUserPrompt(question, context);

        // 6. Build message stream with bounded conversation history
        var messages = new List<LLMChatMessage>();
        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            var boundedHistory = conversationHistory
                .TakeLast(_ragOptions.MaxConversationMessages)
                .ToList();
            messages.AddRange(boundedHistory);
        }
        messages.Add(new LLMChatMessage("user", userPrompt));

        var llmRequest = new LLMRequest(
            Messages: messages,
            SystemPrompt: systemPrompt,
            Context: context,
            Temperature: _llmOptions.Temperature,
            MaxTokens: _llmOptions.MaxTokens,
            Model: _llmOptions.Model);

        try
        {
            var llmResponse = await _llmService.ChatAsync(llmRequest, cancellationToken);

            // Validate and sanitize citations: detect fabricated references, strip invalid tags, preserve authoritative sources
            var citationResult = _citationValidator.ValidateAndSanitize(llmResponse.Content, sources);

            return Result.Success(new RagAnswerResult(
                Answer: citationResult.SanitizedText,
                Sources: sources,
                PromptTokens: llmResponse.PromptTokens,
                CompletionTokens: llmResponse.CompletionTokens,
                TotalTokens: llmResponse.TotalTokens,
                CitedSources: citationResult.CitedSources));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RAG LLM execution was canceled for query: {Query}", question);
            return Result.Failure<RagAnswerResult>(new Error("AI.Cancelled", "AI request was cancelled."));
        }
        catch (LlmConfigurationException ex)
        {
            _logger.LogError(ex, "LLM configuration error during RAG");
            return Result.Failure<RagAnswerResult>(new Error("AI.NotConfigured", ex.Message));
        }
        catch (LlmException ex)
        {
            _logger.LogError(ex, "LLM provider error during RAG");
            return Result.Failure<RagAnswerResult>(new Error("AI.ProviderError", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during RAG LLM call");
            return Result.Failure<RagAnswerResult>(new Error("AI.ExecutionError", "Failed to generate answer from AI service."));
        }
    }

    // Legacy method for backward compatibility
    public async Task<RagResponse> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default)
    {
        var result = await AnswerQuestionAsync(request.WorkspaceId, request.Query, null, cancellationToken);
        if (!result.IsSuccess)
        {
            return new RagResponse(result.Error.Description, Array.Empty<SourceReferenceDto>(), 0, 0);
        }

        var citations = result.Value.Sources.Select(s => new SourceReferenceDto(
            s.DocumentId,
            s.DocumentChunkId,
            s.PageId,
            s.NoteId,
            s.Title,
            s.Snippet ?? string.Empty,
            s.RelevanceScore,
            s.PageNumber ?? 1)).ToList();

        return new RagResponse(
            result.Value.Answer,
            citations,
            result.Value.PromptTokens ?? 0,
            result.Value.CompletionTokens ?? 0);
    }
}
