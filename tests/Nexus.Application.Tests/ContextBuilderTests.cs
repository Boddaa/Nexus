using Nexus.Application.DTOs.Conversations;
using Nexus.Application.Features.AI.Services;
using Xunit;

namespace Nexus.Application.Tests;

public class ContextBuilderTests
{
    private readonly ContextBuilder _builder = new();

    [Fact]
    public void BuildContext_NullOrEmptySources_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, _builder.BuildContext(null!));
        Assert.Equal(string.Empty, _builder.BuildContext(Array.Empty<ChatSourceDto>()));
    }

    [Fact]
    public void BuildContext_SingleSource_FormatsAllFields()
    {
        var source = new ChatSourceDto(
            Id: Guid.NewGuid(),
            DocumentId: Guid.NewGuid(),
            DocumentChunkId: Guid.NewGuid(),
            PageId: null,
            NoteId: null,
            Title: "Architecture Overview",
            SourceType: "Document",
            RelevanceScore: 0.95,
            PageNumber: 3,
            Snippet: "NEXUS uses a clean architecture modular monolith.");

        var context = _builder.BuildContext(new[] { source }, maxCharacters: 1000);

        Assert.Contains("[SOURCE 1]", context);
        Assert.Contains("Title: Architecture Overview", context);
        Assert.Contains("Type: Document", context);
        Assert.Contains("Page: 3", context);
        Assert.Contains("Content:", context);
        Assert.Contains("NEXUS uses a clean architecture modular monolith.", context);
    }

    [Fact]
    public void BuildContext_MultipleSources_FormatsInSequentialOrder()
    {
        var sources = new List<ChatSourceDto>
        {
            new(Guid.NewGuid(), null, null, Guid.NewGuid(), null, "Page 1", "Page", 0.9, null, "First snippet"),
            new(Guid.NewGuid(), null, null, null, Guid.NewGuid(), "Note 2", "Note", 0.8, null, "Second snippet")
        };

        var context = _builder.BuildContext(sources, maxCharacters: 2000);

        Assert.Contains("[SOURCE 1]", context);
        Assert.Contains("Title: Page 1", context);
        Assert.Contains("[SOURCE 2]", context);
        Assert.Contains("Title: Note 2", context);
        Assert.True(context.IndexOf("[SOURCE 1]") < context.IndexOf("[SOURCE 2]"));
    }

    [Fact]
    public void BuildContext_ExceedsMaxCharacters_TruncatesCleanly()
    {
        var sources = new List<ChatSourceDto>();
        for (int i = 1; i <= 10; i++)
        {
            sources.Add(new ChatSourceDto(
                Guid.NewGuid(), null, null, null, null,
                $"Document {i}", "Document", 0.8, i,
                new string('X', 500)));
        }

        var context = _builder.BuildContext(sources, maxCharacters: 800);

        Assert.True(context.Length <= 850);
        Assert.Contains("[SOURCE 1]", context);
    }
}
