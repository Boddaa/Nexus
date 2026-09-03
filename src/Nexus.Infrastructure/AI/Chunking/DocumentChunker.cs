using System.Text.RegularExpressions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.Chunking;

public class DocumentChunker : IDocumentChunker
{
    private readonly ChunkingOptions _defaultOptions;

    public DocumentChunker(ChunkingOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new ChunkingOptions();
    }

    public IReadOnlyList<ChunkResult> ChunkText(string text, ChunkingOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<ChunkResult>();
        }

        var effectiveOptions = options ?? _defaultOptions;
        var chunkSize = Math.Max(100, effectiveOptions.ChunkSize);
        var overlap = Math.Max(0, Math.Min(effectiveOptions.ChunkOverlap, chunkSize - 50));

        // Normalize line endings and multiple whitespaces
        var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalizedText = Regex.Replace(normalizedText, @"[ \t]+", " ").Trim();

        if (normalizedText.Length == 0)
        {
            return Array.Empty<ChunkResult>();
        }

        // If total text fits in a single chunk
        if (normalizedText.Length <= chunkSize)
        {
            return new[]
            {
                new ChunkResult(0, normalizedText, 0, normalizedText.Length)
            };
        }

        var chunks = new List<ChunkResult>();
        int currentIndex = 0;
        int chunkCounter = 0;

        while (currentIndex < normalizedText.Length)
        {
            int remainingLength = normalizedText.Length - currentIndex;
            if (remainingLength <= chunkSize)
            {
                var finalChunkText = normalizedText.Substring(currentIndex).Trim();
                if (finalChunkText.Length > 0)
                {
                    chunks.Add(new ChunkResult(chunkCounter, finalChunkText, currentIndex, normalizedText.Length));
                }
                break;
            }

            // Determine candidate end
            int candidateEnd = currentIndex + chunkSize;

            // Search backwards for a natural sentence/paragraph boundary
            int naturalBoundary = FindNaturalBoundary(normalizedText, candidateEnd, Math.Min(120, chunkSize / 4));
            int actualEnd = (naturalBoundary > currentIndex) ? naturalBoundary : candidateEnd;

            var chunkText = normalizedText.Substring(currentIndex, actualEnd - currentIndex).Trim();
            if (chunkText.Length > 0)
            {
                chunks.Add(new ChunkResult(chunkCounter++, chunkText, currentIndex, actualEnd));
            }

            // Advance start index using overlap
            int nextIndex = actualEnd - overlap;
            if (nextIndex <= currentIndex)
            {
                nextIndex = currentIndex + chunkSize;
            }

            // Advance past leading whitespace
            while (nextIndex < normalizedText.Length && char.IsWhiteSpace(normalizedText[nextIndex]))
            {
                nextIndex++;
            }

            currentIndex = nextIndex;
        }

        return chunks;
    }

    private static int FindNaturalBoundary(string text, int targetIndex, int maxLookback)
    {
        int minIndex = Math.Max(0, targetIndex - maxLookback);

        // Priority 1: Paragraph break (\n\n or \n)
        for (int i = targetIndex - 1; i >= minIndex; i--)
        {
            if (text[i] == '\n')
            {
                return i + 1;
            }
        }

        // Priority 2: Sentence punctuation (. , ! ?) followed by whitespace
        for (int i = targetIndex - 1; i >= minIndex; i--)
        {
            if ((text[i] == '.' || text[i] == '!' || text[i] == '?' || text[i] == ';') &&
                (i + 1 < text.Length && char.IsWhiteSpace(text[i + 1])))
            {
                return i + 1;
            }
        }

        // Priority 3: Word boundary (space)
        for (int i = targetIndex - 1; i >= minIndex; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        return targetIndex;
    }
}
