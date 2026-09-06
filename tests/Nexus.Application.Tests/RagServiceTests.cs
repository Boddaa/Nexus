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
        public string ResponseToReturn { get; set; } = "Grounded response based on [SOURCE 1].";

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
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

    [Fact]
    public async Task AnswerQuestionAsync_EmptyQuestion_ReturnsValidationError()
    {
        var searchService = new MockSearchService();
        var llmService = new MockLlmService();
        var rag = new RagService(searchService, new ContextBuilder(), new PromptBuilder(), llmService,
            Options.Create(new RagOptions()), Options.Create(new LlmOptions()), NullLogger<RagService>.Instance);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Rag.EmptyQuestion", result.Error.Code);
    }

    [Fact]
    public async Task AnswerQuestionAsync_NoSourcesFound_ReturnsGroundedInsufficientNoticeWithoutCallingLlm()
    {
        var searchService = new MockSearchService { ItemsToReturn = new List<SearchResultDto>() };
        var llmService = new MockLlmService { ShouldThrowProvider = true }; // Should never be called!
        var rag = new RagService(searchService, new ContextBuilder(), new PromptBuilder(), llmService,
            Options.Create(new RagOptions()), Options.Create(new LlmOptions()), NullLogger<RagService>.Instance);

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
                new(Guid.NewGuid(), "Document", Guid.NewGuid(), null, "Security Manual", "Always lock workstations.", 85.0, DateTime.UtcNow, null, Guid.NewGuid(), "Hybrid", 0.85, 2),
                new(Guid.NewGuid(), "Note", Guid.NewGuid(), null, "Low relevance", "Random snippet", 0.01, DateTime.UtcNow, null) // Below threshold
            }
        };

        var llmService = new MockLlmService { ResponseToReturn = "According to [SOURCE 1], workstations must be locked." };
        var rag = new RagService(searchService, new ContextBuilder(), new PromptBuilder(), llmService,
            Options.Create(new RagOptions { MinimumRelevanceScore = 0.05 }), Options.Create(new LlmOptions()), NullLogger<RagService>.Instance);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "What are the security guidelines?");

        Assert.True(result.IsSuccess);
        Assert.Equal("According to [SOURCE 1], workstations must be locked.", result.Value.Answer);
        Assert.Single(result.Value.Sources);
        Assert.Equal("Security Manual", result.Value.Sources[0].Title);
        Assert.Equal(2, result.Value.Sources[0].PageNumber);
    }

    [Fact]
    public async Task AnswerQuestionAsync_LlmProviderError_ReturnsMeaningfulFailure()
    {
        var searchService = new MockSearchService
        {
            ItemsToReturn = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", Guid.NewGuid(), null, "Doc 1", "Content", 90.0, DateTime.UtcNow, null)
            }
        };

        var llmService = new MockLlmService { ShouldThrowProvider = true };
        var rag = new RagService(searchService, new ContextBuilder(), new PromptBuilder(), llmService,
            Options.Create(new RagOptions()), Options.Create(new LlmOptions()), NullLogger<RagService>.Instance);

        var result = await rag.AnswerQuestionAsync(Guid.NewGuid(), "Explain this document");

        Assert.False(result.IsSuccess);
        Assert.Equal("AI.ProviderError", result.Error.Code);
    }
}
