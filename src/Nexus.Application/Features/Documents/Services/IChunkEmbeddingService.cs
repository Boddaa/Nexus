using Nexus.Application.DTOs.Documents;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Documents.Services;

public interface IChunkEmbeddingService
{
    Task<Result<GenerateEmbeddingsResponse>> GenerateEmbeddingsForDocumentAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> GeneratePendingEmbeddingsAsync(
        Guid workspaceId,
        int batchSize = 25,
        CancellationToken cancellationToken = default);
}
