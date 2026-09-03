using Nexus.Application.Common.Options;
using Nexus.Infrastructure.AI.Chunking;
using Xunit;

namespace Nexus.Infrastructure.Tests;

public class DocumentChunkerTests
{
    private readonly DocumentChunker _chunker = new();

    [Fact]
    public void ChunkText_EmptyOrWhitespace_ReturnsEmptyList()
    {
        Assert.Empty(_chunker.ChunkText(""));
        Assert.Empty(_chunker.ChunkText("    \n\t   "));
        Assert.Empty(_chunker.ChunkText(null!));
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunkWithZeroIndex()
    {
        var text = "This is a short document that easily fits in a single chunk.";
        var chunks = _chunker.ChunkText(text, new ChunkingOptions { ChunkSize = 500 });

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(text, chunks[0].Text);
        Assert.Equal(0, chunks[0].StartPosition);
        Assert.Equal(text.Length, chunks[0].EndPosition);
    }

    [Fact]
    public void ChunkText_LongText_ProducesMultipleOrderedChunks()
    {
        var paragraphs = new List<string>();
        for (int i = 1; i <= 20; i++)
        {
            paragraphs.Add($"Paragraph {i}: NEXUS is an AI Knowledge and Learning Workspace built with .NET 10, clean architecture, and modular monolith principles.");
        }
        var fullText = string.Join("\n\n", paragraphs);

        var options = new ChunkingOptions { ChunkSize = 300, ChunkOverlap = 50 };
        var chunks = _chunker.ChunkText(fullText, options);

        Assert.True(chunks.Count > 1);

        // Check sequential ordering
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkIndex);
            Assert.False(string.IsNullOrWhiteSpace(chunks[i].Text));
        }
    }

    [Fact]
    public void ChunkText_Deterministic_ProducesIdenticalChunksOnRepeatedRuns()
    {
        var text = "Deterministic testing is essential for RAG systems. " +
                   "Every chunk boundary must remain constant when text is unchanged. " +
                   "Here is additional content to make the document exceed the chunk size threshold.";

        var options = new ChunkingOptions { ChunkSize = 70, ChunkOverlap = 15 };

        var run1 = _chunker.ChunkText(text, options);
        var run2 = _chunker.ChunkText(text, options);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].ChunkIndex, run2[i].ChunkIndex);
            Assert.Equal(run1[i].Text, run2[i].Text);
            Assert.Equal(run1[i].StartPosition, run2[i].StartPosition);
            Assert.Equal(run1[i].EndPosition, run2[i].EndPosition);
        }
    }

    [Fact]
    public void ChunkText_NoEmptyChunksProduced()
    {
        var text = "Word1. Word2.\n\n\n\n\nWord3. Word4.     \n\n   Word5.";
        var options = new ChunkingOptions { ChunkSize = 25, ChunkOverlap = 5 };

        var chunks = _chunker.ChunkText(text, options);

        Assert.NotEmpty(chunks);
        foreach (var chunk in chunks)
        {
            Assert.False(string.IsNullOrWhiteSpace(chunk.Text));
        }
    }

    [Fact]
    public void ChunkText_PreservesNaturalSentenceBoundaries()
    {
        var sentence1 = "First full sentence about architecture design.";
        var sentence2 = "Second complete sentence about persistence patterns.";
        var sentence3 = "Third distinct sentence discussing vector search.";
        var fullText = $"{sentence1} {sentence2} {sentence3}";

        var options = new ChunkingOptions { ChunkSize = 65, ChunkOverlap = 15 };
        var chunks = _chunker.ChunkText(fullText, options);

        Assert.True(chunks.Count >= 2);
        // The first chunk should end neatly at sentence boundary rather than cutting a word
        Assert.EndsWith(".", chunks[0].Text);
    }
}
