using Nexus.Application.DTOs.Documents;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Documents.Services;

public interface IDocumentChunkService
{
    Task<Result<IReadOnlyList<DocumentChunkDto>>> ChunkDocumentAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DocumentChunkDto>>> GetDocumentChunksAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
