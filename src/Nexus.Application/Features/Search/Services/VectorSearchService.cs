using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.Common.Utilities;
using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Search.Services;

/// <summary>
/// Implements vector similarity search across workspace document chunks.
/// Note: In Phase 3 v1, embeddings are stored as compact varbinary in SQL Server,
/// with strict database-side workspace isolation and bounded candidate projection,
/// followed by application-side cosine similarity calculation.
/// </summary>
public class VectorSearchService : IVectorSearchService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOptions<HybridSearchOptions> _options;

    public VectorSearchService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IOptions<HybridSearchOptions> options)
    {
        _context = context;
        _currentUserService = currentUserService;
        _options = options;
    }

    public async Task<Result<IReadOnlyList<VectorSearchResultDto>>> SearchSimilarChunksAsync(
        Guid workspaceId,
        float[] queryEmbedding,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<VectorSearchResultDto>>(Error.Unauthorized);
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, currentUserId.Value, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<VectorSearchResultDto>>(Error.Unauthorized);
        }

        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
            return Result.Failure<IReadOnlyList<VectorSearchResultDto>>(
                new Error("Search.InvalidQueryVector", "Query embedding cannot be empty."));
        }

        var maxTopK = _options.Value.MaxTopK > 0 ? _options.Value.MaxTopK : 50;
        var effectiveTopK = Math.Clamp(topK, 1, maxTopK);

        // Bounded candidate retrieval to prevent loading all workspace chunks into memory (Patch Rule 1)
        var candidateLimit = Math.Clamp(effectiveTopK * 10, 50, 300);

        // 1. Database-side filtering for workspace, status, and soft-delete exclusion (Patch Rule 5)
        var candidateChunks = await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId &&
                        !c.IsDeleted &&
                        !c.Document.IsDeleted &&
                        c.EmbeddingStatus == EmbeddingStatus.Completed &&
                        c.EmbeddingVector != null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.WorkspaceId,
                c.ChunkIndex,
                c.Text,
                c.PageNumber,
                c.EmbeddingVector,
                c.CreatedAtUtc,
                DocumentTitle = c.Document.Title
            })
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);

        if (candidateChunks.Count == 0)
        {
            return Result.Success<IReadOnlyList<VectorSearchResultDto>>(Array.Empty<VectorSearchResultDto>());
        }

        // 2. Compute cosine similarity over the bounded candidate set
        var scoredCandidates = new List<VectorSearchResultDto>(candidateChunks.Count);
        foreach (var c in candidateChunks)
        {
            var chunkVector = VectorMath.BytesToVector(c.EmbeddingVector);
            if (chunkVector.Length != queryEmbedding.Length)
            {
                // Skip chunks with dimension mismatch (e.g. from an old model)
                continue;
            }

            var similarity = VectorMath.CosineSimilarity(queryEmbedding, chunkVector);

            scoredCandidates.Add(new VectorSearchResultDto(
                c.DocumentId,
                c.Id,
                c.WorkspaceId,
                c.DocumentTitle,
                c.ChunkIndex,
                c.Text,
                c.PageNumber,
                similarity,
                c.CreatedAtUtc));
        }

        // 3. Order by SimilarityScore descending, then CreatedAtUtc descending, and take Top-K
        var topResults = scoredCandidates
            .OrderByDescending(r => r.SimilarityScore)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Take(effectiveTopK)
            .ToList();

        return Result.Success<IReadOnlyList<VectorSearchResultDto>>(topResults);
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                (w.OwnerId == userId || w.Members.Any(m => m.UserId == userId && !m.IsDeleted)), cancellationToken);
    }
}
