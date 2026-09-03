namespace Nexus.Application.DTOs.Documents;

public record DocumentChunkDto(
    Guid Id,
    Guid DocumentId,
    Guid WorkspaceId,
    int ChunkIndex,
    string Text,
    int StartPosition,
    int EndPosition,
    int? PageNumber,
    string EmbeddingStatus,
    string? EmbeddingModel,
    int? EmbeddingDimensions,
    bool HasEmbedding,
    DateTime CreatedAtUtc);

public record ChunkDocumentResponse(
    Guid DocumentId,
    int TotalChunksCreated);

public record GenerateEmbeddingsResponse(
    Guid DocumentId,
    int TotalEmbeddingsGenerated);
