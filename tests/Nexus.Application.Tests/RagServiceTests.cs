using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Search;
using Nexus.Application.Features.AI.Services;
using Nexus.Application.Features.Search.Services;
using Nexus.Domain.Common;
using Xunit;
using SearchRequest = Nexus.Application.DTOs.Search.SearchRequest;

namespace Nexus.Application.Tests;

public class RagServiceTests
{
    private class MockSearchService : ISearchService
    {
        public bool ShouldFail { get; set; } = false;
        public List<SearchResultDto> ItemsToReturn { get; set; } = new();

        public Task<Result<PagedResult<SearchResultDto>>> SearchAsync(Guid workspaceId, SearchRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult(Result.Failure<PagedResult<SearchResultDto>>(new Error("Search.Failed", "Database error.")));
            }

            var paged = new PagedResult<SearchResultDto>(ItemsToReturn, 1, 20, ItemsToReturn.Count);
            return Task.FromResult(Result.Success(paged));
        }
    }

    private class MockLlmService : ILLMService
    {
        public bool ShouldThrowConfig { get; set; } = false;
        public bool ShouldThrowProvider { get; set; } = false;
        public bool ShouldThrowCancelled { get; set; } = false;
        public string ResponseToReturn { get; set; } = "Grounded response based on [SOURCE 1].";

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested || ShouldThrowCancelled)
            {
                throw new OperationCanceledException();
            }

            if (ShouldThrowConfig) throw new LlmConfigurationException("LLM not configured.");
            if (ShouldThrowProvider) throw new LlmException("Provider unreachable.");

            return Task.FromResult(new LLMResponse(
                Content: ResponseToReturn,
                Model: "mock-model",
                PromptTokens: 100,
                CompletionTokens: 50,
                TotalTokens: 150));
        }
    }

    private static RagService CreateRagService(
        ISearchService searchService,
        ILLMService llmService,
        RagOptions? ragOptions = null,
        LlmOptions? llmOptions = null)
    {
        return new RagService(
            searchService,
            new ContextBuilder(),
            new PromptBuilder(),
            new CitationValidator(),
            llmService,
            Options.Create(ragOptions ?? new RagOptions()),
            Options.Create(llmOptions ?? new LlmOptions()),
            NullLogger<RagService>.Instance);
    }

    [Fact]
    public async Task AnswerQuestionAsync_EmptyQuestion_ReturnsValidationError()
    {
        var searchService = new MockSearchService();
        var llmService = new MockLlmService();
        var rag = CreateRagService(searchService, llmService);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Rag.EmptyQuestion", result.Error.Code);
    }

    [Fact]
    public async Task AnswerQuestionAsync_NoSourcesFound_ReturnsGroundedInsufficientNoticeWithoutCallingLlm()
    {
        var searchService = new MockSearchService { ItemsToReturn = new List<SearchResultDto>() };
        var llmService = new MockLlmService { ShouldThrowProvider = true }; // Should never be called!
        var rag = CreateRagService(searchService, llmService);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "What is the policy?");

        Assert.True(result.IsSuccess);
        Assert.Contains("could not find any relevant information", result.Value.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Value.Sources);
    }

    [Fact]
    public async Task AnswerQuestionAsync_SourcesAboveThreshold_BuildsContextAndCallsLlm()
    {
        var searchService = new MockSearchService
        {
            ItemsToReturn = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", Guid.NewGuid(), null, "Security Manual", "Always lock workstations.", 85.0, DateTime.UtcNow, null, Guid.NewGuid(), "Hybrid", 0.85, 2, 0.85),
                new(Guid.NewGuid(), "Note", Guid.NewGuid(), null, "Low relevance", "Random snippet", 1.0, DateTime.UtcNow, null, null, "Keyword", null, null, 0.01) // Below threshold
            }
        };

        var llmService = new MockLlmService { ResponseToReturn = "According to [SOURCE 1], workstations must be locked." };
        var rag = CreateRagService(searchService, llmService, new RagOptions { MinimumRelevanceScore = 0.05 });

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "What are the security guidelines?");

        Assert.True(result.IsSuccess);
        Assert.Equal("According to [SOURCE 1], workstations must be locked.", result.Value.Answer);
        Assert.Single(result.Value.Sources);
        Assert.Equal("Security Manual", result.Value.Sources[0].Title);
        Assert.Equal(2, result.Value.Sources[0].PageNumber);
        Assert.Equal(0.85, result.Value.Sources[0].RelevanceScore);
    }

    [Fact]
    public async Task AnswerQuestionAsync_ModeSpecificRelevance_KeywordNormalizedScoreHandledCorrectly()
    {
        // For Keyword mode, raw score might be e.g. 150, but NormalizedScore is 0.80
        var searchService = new MockSearchService
        {
            ItemsToReturn = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Page", Guid.NewGuid(), null, "High Match", "Exact keywords.", 150.0, DateTime.UtcNow, null, null, "Keyword", null, null, 0.80),
                new(Guid.NewGuid(), "Page", Guid.NewGuid(), null, "Low Match", "Weak keyword.", 35.0, DateTime.UtcNow, null, null, "Keyword", null, null, 0.20)
            }
        };

        var llmService = new MockLlmService { ResponseToReturn = "High match details from [SOURCE 1]." };
        // Set threshold to 0.50 -> High Match (0.80) should pass, Low Match (0.20) filtered out
        var rag = CreateRagService(searchService, llmService, new RagOptions { MinimumRelevanceScore = 0.50 });

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "High Match query");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Sources);
        Assert.Equal("High Match", result.Value.Sources[0].Title);
        Assert.Equal(0.80, result.Value.Sources[0].RelevanceScore);
    }

    [Fact]
    public async Task AnswerQuestionAsync_FabricatedCitation_SanitizesAnswerAndPreservesAuthoritativeSources()
    {
        var searchService = new MockSearchService
        {
            ItemsToReturn = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", Guid.NewGuid(), null, "Real Document", "Fact A.", 80.0, DateTime.UtcNow, null, Guid.NewGuid(), "Semantic", 0.80, 1, 0.80)
            }
        };

        // LLM cites [SOURCE 1] (valid) and [SOURCE 99] (fabricated)
        var llmService = new MockLlmService { ResponseToReturn = "Fact A is verified by [SOURCE 1], but [SOURCE 99] is fictitious." };
        var rag = CreateRagService(searchService, llmService);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "Tell me facts");

        Assert.True(result.IsSuccess);
        // [SOURCE 99] stripped from sanitized answer
        Assert.DoesNotContain("[SOURCE 99]", result.Value.Answer);
        Assert.Contains("[SOURCE 1]", result.Value.Answer);
        // Authoritative sources preserved
        Assert.Single(result.Value.Sources);
        Assert.Equal("Real Document", result.Value.Sources[0].Title);
    }

    [Fact]
    public async Task AnswerQuestionAsync_LlmProviderError_ReturnsMeaningfulFailure()
    {
        var searchService = new MockSearchService
        {
            ItemsToReturn = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", Guid.NewGuid(), null, "Doc 1", "Content", 90.0, DateTime.UtcNow, null, null, "Semantic", 0.90, null, 0.90)
            }
        };

        var llmService = new MockLlmService { ShouldThrowProvider = true };
        var rag = CreateRagService(searchService, llmService);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "Explain this document");

        Assert.False(result.IsSuccess);
        Assert.Equal("AI.ProviderError", result.Error.Code);
    }

    [Fact]
    public async Task AnswerQuestionAsync_LlmCancellation_HandlesGracefully()
    {
        var searchService = new MockSearchService
        {
            ItemsToReturn = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", Guid.NewGuid(), null, "Doc 1", "Content", 90.0, DateTime.UtcNow, null, null, "Semantic", 0.90, null, 0.90)
            }
        };

        var llmService = new MockLlmService { ShouldThrowCancelled = true };
        var rag = CreateRagService(searchService, llmService);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "Explain this document", cancellationToken: cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("AI.Cancelled", result.Error.Code);
    }
}
