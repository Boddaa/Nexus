using Nexus.Application.DTOs.Search;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Search.Services;

public interface ISearchService
{
    Task<Result<PagedResult<SearchResultDto>>> SearchAsync(
        Guid workspaceId,
        SearchRequest request,
        CancellationToken cancellationToken = default);
}
