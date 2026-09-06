using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Application.Features.VisualThinking.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/boards")]
public class BoardsController : ApiControllerBase
{
    private readonly IBoardService _boardService;

    public BoardsController(IBoardService boardService)
    {
        _boardService = boardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBoards(Guid workspaceId, CancellationToken ct)
    {
        var result = await _boardService.GetBoardsAsync(workspaceId, ct);
        return HandleResult(result);
    }

    [HttpGet("{boardId:guid}")]
    public async Task<IActionResult> GetBoardById(Guid workspaceId, Guid boardId, CancellationToken ct)
    {
        var result = await _boardService.GetBoardByIdAsync(workspaceId, boardId, ct);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard(Guid workspaceId, [FromBody] CreateBoardRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _boardService.CreateBoardAsync(workspaceId, request, ct);
        return HandleCreatedResult(result, nameof(GetBoardById), new { workspaceId, boardId = result.Value?.Id });
    }

    [HttpPut("{boardId:guid}")]
    public async Task<IActionResult> UpdateBoard(Guid workspaceId, Guid boardId, [FromBody] UpdateBoardRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _boardService.UpdateBoardAsync(workspaceId, boardId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{boardId:guid}")]
    public async Task<IActionResult> DeleteBoard(Guid workspaceId, Guid boardId, CancellationToken ct)
    {
        var result = await _boardService.DeleteBoardAsync(workspaceId, boardId, ct);
        return HandleResult(result);
    }

    // ==================== Items ====================

    [HttpGet("{boardId:guid}/items")]
    public async Task<IActionResult> GetBoardItems(Guid workspaceId, Guid boardId, CancellationToken ct)
    {
        var result = await _boardService.GetBoardItemsAsync(workspaceId, boardId, ct);
        return HandleResult(result);
    }

    [HttpGet("{boardId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> GetBoardItemById(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken ct)
    {
        var result = await _boardService.GetBoardItemByIdAsync(workspaceId, boardId, itemId, ct);
        return HandleResult(result);
    }

    [HttpPost("{boardId:guid}/items")]
    public async Task<IActionResult> CreateBoardItem(Guid workspaceId, Guid boardId, [FromBody] CreateBoardItemRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _boardService.CreateBoardItemAsync(workspaceId, boardId, request, ct);
        return HandleCreatedResult(result, nameof(GetBoardItemById), new { workspaceId, boardId, itemId = result.Value?.Id });
    }

    [HttpPut("{boardId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateBoardItem(Guid workspaceId, Guid boardId, Guid itemId, [FromBody] UpdateBoardItemRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _boardService.UpdateBoardItemAsync(workspaceId, boardId, itemId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{boardId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteBoardItem(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken ct)
    {
        var result = await _boardService.DeleteBoardItemAsync(workspaceId, boardId, itemId, ct);
        return HandleResult(result);
    }

    [HttpPatch("{boardId:guid}/items/batch")]
    public async Task<IActionResult> BatchUpdateBoardItems(Guid workspaceId, Guid boardId, [FromBody] BatchUpdateBoardItemsRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _boardService.BatchUpdateBoardItemsAsync(workspaceId, boardId, request, ct);
        return HandleResult(result);
    }
}
