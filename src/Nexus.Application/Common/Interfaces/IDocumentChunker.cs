using Nexus.Application.Common.Options;

namespace Nexus.Application.Common.Interfaces;

public record ChunkResult(
    int ChunkIndex,
    string Text,
    int StartPosition,
    int EndPosition,
    int? PageNumber = null);

public interface IDocumentChunker
{
    IReadOnlyList<ChunkResult> ChunkText(
        string text,
        ChunkingOptions? options = null);
}
