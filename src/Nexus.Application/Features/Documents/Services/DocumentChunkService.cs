using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Documents;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Documents.Services;

public class DocumentChunkService : IDocumentChunkService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentChunker _chunker;
    private readonly IOptions<ChunkingOptions> _options;

    public DocumentChunkService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IDocumentChunker chunker,
        IOptions<ChunkingOptions> options)
    {
        _context = context;
        _currentUserService = currentUserService;
        _chunker = chunker;
        _options = options;
    }

    public async Task<Result<IReadOnlyList<DocumentChunkDto>>> ChunkDocumentAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.Unauthorized);
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, currentUserId.Value, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.Unauthorized);
        }

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);

        if (document == null)
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.NotFound);
        }

        if (string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(
                new Error("Document.NoText", "The document contains no extracted text to chunk."));
        }

        // 1. Generate deterministic chunks using the chunker
        var chunkResults = _chunker.ChunkText(document.ExtractedText, _options.Value);
        if (chunkResults.Count == 0)
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(
                new Error("Document.ChunkingFailed", "No chunks could be produced from document text."));
        }

        // 2. Re-chunking reconciliation: Remove obsolete chunks transactionally (Patch Rule 4)
        var existingChunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);

        if (existingChunks.Count > 0)
        {
            _context.DocumentChunks.RemoveRange(existingChunks);
        }

        // 3. Create new chunks with Pending embedding status
        var newChunks = new List<DocumentChunk>(chunkResults.Count);
        foreach (var r in chunkResults)
        {
            newChunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                WorkspaceId = workspaceId,
                ChunkIndex = r.ChunkIndex,
                Text = r.Text,
                StartPosition = r.StartPosition,
                EndPosition = r.EndPosition,
                PageNumber = r.PageNumber ?? (document.PageCount > 0 ? 1 : null),
                EmbeddingStatus = EmbeddingStatus.Pending
            });
        }

        _context.DocumentChunks.AddRange(newChunks);
        await _context.SaveChangesAsync(cancellationToken);

        var dtos = newChunks.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<DocumentChunkDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<DocumentChunkDto>>> GetDocumentChunksAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.Unauthorized);
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, currentUserId.Value, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.Unauthorized);
        }

        var chunks = await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId && !c.IsDeleted)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

        var dtos = chunks.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<DocumentChunkDto>>(dtos);
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                (w.OwnerId == userId || w.Members.Any(m => m.UserId == userId && !m.IsDeleted)), cancellationToken);
    }

    private static DocumentChunkDto MapToDto(DocumentChunk c)
    {
        return new DocumentChunkDto(
            c.Id,
            c.DocumentId,
            c.WorkspaceId,
            c.ChunkIndex,
            c.Text,
            c.StartPosition,
            c.EndPosition,
            c.PageNumber,
            c.EmbeddingStatus.ToString(),
            c.EmbeddingModel,
            c.EmbeddingDimensions,
            c.EmbeddingVector != null && c.EmbeddingVector.Length > 0,
            c.CreatedAtUtc);
    }
}
