using Nexus.Application.DTOs.Pages;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Pages.Services;

public interface IPageService
{
    Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default);
}
