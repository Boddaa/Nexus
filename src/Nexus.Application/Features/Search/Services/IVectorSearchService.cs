using Nexus.Domain.Common;

namespace Nexus.Application.Features.Search.Services;

public record VectorSearchResultDto(
    Guid DocumentId,
    Guid ChunkId,
    Guid WorkspaceId,
    string DocumentTitle,
    int ChunkIndex,
    string Text,
    int? PageNumber,
    double SimilarityScore,
    DateTime CreatedAtUtc);

public interface IVectorSearchService
{
    Task<Result<IReadOnlyList<VectorSearchResultDto>>> SearchSimilarChunksAsync(
        Guid workspaceId,
        float[] queryEmbedding,
        int topK = 10,
        CancellationToken cancellationToken = default);
}
