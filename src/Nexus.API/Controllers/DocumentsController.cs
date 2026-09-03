using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.Features.Documents.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/documents")]
public class DocumentsController : ApiControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        Guid workspaceId,
        [FromQuery] Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetDocumentsAsync(workspaceId, pageId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{documentId:guid}", Name = nameof(GetDocumentById))]
    public async Task<IActionResult> GetDocumentById(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetDocumentByIdAsync(workspaceId, documentId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    [RequestSizeLimit(52428800)] // 50 MB
    public async Task<IActionResult> UploadDocument(
        Guid workspaceId,
        IFormFile file,
        [FromForm] string? title = null,
        [FromForm] Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Code = "Documents.EmptyFile", Description = "Uploaded file cannot be empty." });
        }

        await using var stream = file.OpenReadStream();
        var request = new UploadDocumentStreamRequest(
            stream,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length,
            title,
            pageId);

        var result = await _documentService.UploadAsync(workspaceId, request, cancellationToken);
        if (result.IsSuccess)
        {
            return HandleCreatedResult(result, nameof(GetDocumentById), new { workspaceId, documentId = result.Value.Id });
        }

        return HandleResult(result);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentService.DeleteAsync(workspaceId, documentId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentService.GetFileStreamAsync(workspaceId, documentId, cancellationToken);
        if (result.IsSuccess)
        {
            return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
        }

        return HandleResult(result);
    }
}
