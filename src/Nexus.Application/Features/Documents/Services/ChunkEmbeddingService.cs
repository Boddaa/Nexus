using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.Common.Utilities;
using Nexus.Application.DTOs.Documents;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Documents.Services;

public class ChunkEmbeddingService : IChunkEmbeddingService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IOptions<EmbeddingOptions> _options;
    private readonly ILogger<ChunkEmbeddingService> _logger;

    public ChunkEmbeddingService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IEmbeddingService embeddingService,
        IOptions<EmbeddingOptions> options,
        ILogger<ChunkEmbeddingService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _embeddingService = embeddingService;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<GenerateEmbeddingsResponse>> GenerateEmbeddingsForDocumentAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<GenerateEmbeddingsResponse>(Error.Unauthorized);
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, currentUserId.Value, cancellationToken))
        {
            return Result.Failure<GenerateEmbeddingsResponse>(Error.Unauthorized);
        }

        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);

        if (document == null)
        {
            return Result.Failure<GenerateEmbeddingsResponse>(Error.NotFound);
        }

        // 1. Query chunks that need embeddings (idempotency check: pending, failed, or stale model/dimensions)
        var model = _options.Value.Model;
        var dimensions = _options.Value.Dimensions;

        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .Where(c => c.EmbeddingStatus != EmbeddingStatus.Completed ||
                        c.EmbeddingModel != model ||
                        c.EmbeddingDimensions != dimensions ||
                        c.EmbeddingVector == null)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            _logger.LogInformation("All chunks for document {DocumentId} already have up-to-date embeddings.", documentId);
            return Result.Success(new GenerateEmbeddingsResponse(documentId, 0));
        }

        // 2. Mark chunks as Processing
        foreach (var chunk in chunks)
        {
            chunk.EmbeddingStatus = EmbeddingStatus.Processing;
            chunk.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);

        int processedCount = 0;
        const int batchSize = 25;

        try
        {
            // 3. Process chunks in batches to avoid single-item HTTP overhead
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.Skip(i).Take(batchSize).ToList();
                var texts = batch.Select(b => b.Text).ToList();

                var vectors = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);

                if (vectors.Count != batch.Count)
                {
                    throw new InvalidOperationException(
                        $"Embedding provider returned {vectors.Count} vectors for a batch of {batch.Count} chunks.");
                }

                for (int j = 0; j < batch.Count; j++)
                {
                    var chunk = batch[j];
                    var vector = vectors[j];

                    // Validate dimension strictly (Patch Rule 3)
                    if (vector.Length != dimensions)
                    {
                        throw new InvalidOperationException(
                            $"Vector dimension mismatch: expected {dimensions}, but received {vector.Length} for chunk {chunk.Id}.");
                    }

                    chunk.EmbeddingVector = VectorMath.VectorToBytes(vector);
                    chunk.EmbeddingModel = model;
                    chunk.EmbeddingDimensions = dimensions;
                    chunk.EmbeddingStatus = EmbeddingStatus.Completed;
                    chunk.UpdatedAtUtc = DateTime.UtcNow;
                    processedCount++;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new GenerateEmbeddingsResponse(documentId, processedCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for document {DocumentId}.", documentId);

            // Mark remaining in-progress chunks as Failed
            foreach (var chunk in chunks.Where(c => c.EmbeddingStatus == EmbeddingStatus.Processing))
            {
                chunk.EmbeddingStatus = EmbeddingStatus.Failed;
                chunk.UpdatedAtUtc = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(CancellationToken.None);

            return Result.Failure<GenerateEmbeddingsResponse>(
                new Error("Embeddings.GenerationFailed", $"Embedding generation failed: {ex.Message}"));
        }
    }

    public async Task<Result<int>> GeneratePendingEmbeddingsAsync(
        Guid workspaceId,
        int batchSize = 25,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<int>(Error.Unauthorized);
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, currentUserId.Value, cancellationToken))
        {
            return Result.Failure<int>(Error.Unauthorized);
        }

        var model = _options.Value.Model;
        var dimensions = _options.Value.Dimensions;

        var pendingChunks = await _context.DocumentChunks
            .Where(c => c.WorkspaceId == workspaceId && !c.IsDeleted && !c.Document.IsDeleted)
            .Where(c => c.EmbeddingStatus == EmbeddingStatus.Pending || c.EmbeddingStatus == EmbeddingStatus.Failed)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pendingChunks.Count == 0)
        {
            return Result.Success(0);
        }

        foreach (var chunk in pendingChunks)
        {
            chunk.EmbeddingStatus = EmbeddingStatus.Processing;
            chunk.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var texts = pendingChunks.Select(c => c.Text).ToList();
            var vectors = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);

            for (int i = 0; i < pendingChunks.Count; i++)
            {
                var chunk = pendingChunks[i];
                var vector = vectors[i];

                if (vector.Length != dimensions)
                {
                    throw new InvalidOperationException($"Vector dimension mismatch for chunk {chunk.Id}.");
                }

                chunk.EmbeddingVector = VectorMath.VectorToBytes(vector);
                chunk.EmbeddingModel = model;
                chunk.EmbeddingDimensions = dimensions;
                chunk.EmbeddingStatus = EmbeddingStatus.Completed;
                chunk.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(pendingChunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate pending embeddings for workspace {WorkspaceId}.", workspaceId);

            foreach (var chunk in pendingChunks.Where(c => c.EmbeddingStatus == EmbeddingStatus.Processing))
            {
                chunk.EmbeddingStatus = EmbeddingStatus.Failed;
                chunk.UpdatedAtUtc = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(CancellationToken.None);

            return Result.Failure<int>(new Error("Embeddings.BatchFailed", ex.Message));
        }
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                (w.OwnerId == userId || w.Members.Any(m => m.UserId == userId && !m.IsDeleted)), cancellationToken);
    }
}
