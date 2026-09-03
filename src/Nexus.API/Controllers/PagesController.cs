using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.Features.Pages.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/pages")]
public class PagesController : ApiControllerBase
{
    private readonly IPageService _pageService;

    public PagesController(IPageService pageService)
    {
        _pageService = pageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPageTree(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await _pageService.GetPageTreeAsync(workspaceId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{pageId:guid}", Name = nameof(GetPageById))]
    public async Task<IActionResult> GetPageById(Guid workspaceId, Guid pageId, CancellationToken cancellationToken)
    {
        var result = await _pageService.GetPageByIdAsync(workspaceId, pageId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePage(Guid workspaceId, [FromBody] CreatePageRequest request, CancellationToken cancellationToken)
    {
        var result = await _pageService.CreatePageAsync(workspaceId, request, cancellationToken);
        if (result.IsSuccess)
        {
            return HandleCreatedResult(result, nameof(GetPageById), new { workspaceId, pageId = result.Value.Id });
        }

        return HandleResult(result);
    }

    [HttpPut("{pageId:guid}")]
    public async Task<IActionResult> UpdatePage(Guid workspaceId, Guid pageId, [FromBody] UpdatePageRequest request, CancellationToken cancellationToken)
    {
        var result = await _pageService.UpdatePageAsync(workspaceId, pageId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("{pageId:guid}/move")]
    public async Task<IActionResult> MovePage(Guid workspaceId, Guid pageId, [FromBody] MovePageRequest request, CancellationToken cancellationToken)
    {
        var result = await _pageService.MovePageAsync(workspaceId, pageId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{pageId:guid}")]
    public async Task<IActionResult> DeletePage(Guid workspaceId, Guid pageId, CancellationToken cancellationToken)
    {
        var result = await _pageService.DeletePageAsync(workspaceId, pageId, cancellationToken);
        return HandleResult(result);
    }
}
