using Nexus.Application.DTOs.Conversations;
using Nexus.Application.Features.AI.Services;
using Xunit;

namespace Nexus.Application.Tests;

public class CitationValidatorTests
{
    private readonly CitationValidator _validator = new();

    private static List<ChatSourceDto> CreateSampleSources(int count)
    {
        var sources = new List<ChatSourceDto>();
        for (int i = 1; i <= count; i++)
        {
            sources.Add(new ChatSourceDto(
                Id: Guid.NewGuid(),
                DocumentId: Guid.NewGuid(),
                DocumentChunkId: Guid.NewGuid(),
                PageId: null,
                NoteId: null,
                Title: $"Document {i}",
                SourceType: "Document",
                RelevanceScore: 0.9,
                PageNumber: i,
                Snippet: $"Sample snippet for document {i}"));
        }
        return sources;
    }

    [Fact]
    public void ValidateAndSanitize_EmptyOrNullContent_ReturnsEmptyResult()
    {
        var sources = CreateSampleSources(2);

        var nullResult = _validator.ValidateAndSanitize(null!, sources);
        Assert.Equal(string.Empty, nullResult.SanitizedText);
        Assert.Empty(nullResult.ValidSourceIndices);
        Assert.Empty(nullResult.InvalidSourceIndices);
        Assert.Empty(nullResult.CitedSources);

        var emptyResult = _validator.ValidateAndSanitize("", sources);
        Assert.Equal(string.Empty, emptyResult.SanitizedText);
    }

    [Fact]
    public void ValidateAndSanitize_ValidCitations_AcceptsAndPreservesCitations()
    {
        var sources = CreateSampleSources(3);
        var content = "According to [SOURCE 1] and [SOURCE 3], the process is deterministic.";

        var result = _validator.ValidateAndSanitize(content, sources);

        Assert.Equal("According to [SOURCE 1] and [SOURCE 3], the process is deterministic.", result.SanitizedText);
        Assert.Equal(new[] { 1, 3 }, result.ValidSourceIndices);
        Assert.Empty(result.InvalidSourceIndices);
        Assert.Equal(2, result.CitedSources.Count);
        Assert.Equal("Document 1", result.CitedSources[0].Title);
        Assert.Equal("Document 3", result.CitedSources[1].Title);
    }

    [Fact]
    public void ValidateAndSanitize_InvalidCitations_StripsTagsAndDetectsInvalidIndices()
    {
        var sources = CreateSampleSources(2);
        var content = "Based on [SOURCE 1] and [SOURCE 99], unauthorized access is denied.";

        var result = _validator.ValidateAndSanitize(content, sources);

        // [SOURCE 99] should be detected as invalid and stripped from sanitized text
        Assert.DoesNotContain("[SOURCE 99]", result.SanitizedText);
        Assert.Contains("[SOURCE 1]", result.SanitizedText);
        Assert.Contains(99, result.InvalidSourceIndices);
        Assert.Contains(1, result.ValidSourceIndices);
        Assert.Single(result.CitedSources);
        Assert.Equal("Document 1", result.CitedSources[0].Title);
    }

    [Fact]
    public void ValidateAndSanitize_FabricatedNumbers_DoesNotCreateSourcesNotRetrievedByNexus()
    {
        var sources = CreateSampleSources(1);
        var content = "According to [SOURCE 5], the moon is made of cheese.";

        var result = _validator.ValidateAndSanitize(content, sources);

        Assert.Empty(result.ValidSourceIndices);
        Assert.Contains(5, result.InvalidSourceIndices);
        Assert.Empty(result.CitedSources); // Never creates authoritative citation for fabricated source
        Assert.DoesNotContain("[SOURCE 5]", result.SanitizedText);
    }

    [Fact]
    public void ValidateAndSanitize_NoCitationsProduced_DoesNotInventCitations()
    {
        var sources = CreateSampleSources(2);
        var content = "Here is a general summary without specific tags.";

        var result = _validator.ValidateAndSanitize(content, sources);

        Assert.Equal("Here is a general summary without specific tags.", result.SanitizedText);
        Assert.Empty(result.ValidSourceIndices);
        Assert.Empty(result.InvalidSourceIndices);
        Assert.Empty(result.CitedSources);
    }

    [Fact]
    public void ValidateAndSanitize_CaseInsensitiveTag_NormalizesAndValidates()
    {
        var sources = CreateSampleSources(2);
        var content = "Reference [source 2] confirms this.";

        var result = _validator.ValidateAndSanitize(content, sources);

        Assert.Equal("Reference [SOURCE 2] confirms this.", result.SanitizedText);
        Assert.Single(result.ValidSourceIndices);
        Assert.Equal(2, result.ValidSourceIndices[0]);
        Assert.Single(result.CitedSources);
        Assert.Equal("Document 2", result.CitedSources[0].Title);
    }
}
