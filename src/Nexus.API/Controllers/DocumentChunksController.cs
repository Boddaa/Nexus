using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.Features.Documents.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/documents/{documentId:guid}")]
public class DocumentChunksController : ApiControllerBase
{
    private readonly IDocumentChunkService _chunkService;
    private readonly IChunkEmbeddingService _embeddingService;

    public DocumentChunksController(
        IDocumentChunkService chunkService,
        IChunkEmbeddingService embeddingService)
    {
        _chunkService = chunkService;
        _embeddingService = embeddingService;
    }

    [HttpPost("chunk")]
    public async Task<IActionResult> ChunkDocument(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _chunkService.ChunkDocumentAsync(workspaceId, documentId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("embed")]
    public async Task<IActionResult> EmbedDocument(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _embeddingService.GenerateEmbeddingsForDocumentAsync(workspaceId, documentId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("chunks")]
    public async Task<IActionResult> GetChunks(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _chunkService.GetDocumentChunksAsync(workspaceId, documentId, cancellationToken);
        return HandleResult(result);
    }
}
