using Nexus.Application.DTOs.Documents;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Documents.Services;

public interface IDocumentService
{
    Task<Result<DocumentDto>> UploadAsync(Guid workspaceId, UploadDocumentStreamRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentSummaryDto>>> GetDocumentsAsync(Guid workspaceId, Guid? pageId = null, CancellationToken cancellationToken = default);
    Task<Result<DocumentDetailDto>> GetDocumentByIdAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<DocumentFileDownloadDto>> GetFileStreamAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
}
