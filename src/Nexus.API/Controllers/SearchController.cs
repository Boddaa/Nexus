using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.Search;
using Nexus.Application.Features.Search.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/search")]
public class SearchController : ApiControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        Guid workspaceId,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest(query ?? string.Empty, page, pageSize, type);
        var result = await _searchService.SearchAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }
}
