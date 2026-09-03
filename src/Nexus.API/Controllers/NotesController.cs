using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.Features.Notes.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/notes")]
public class NotesController : ApiControllerBase
{
    private readonly INoteService _noteService;

    public NotesController(INoteService noteService)
    {
        _noteService = noteService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotes(
        Guid workspaceId,
        [FromQuery] Guid? pageId = null,
        [FromQuery] bool? isPinned = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _noteService.GetNotesAsync(workspaceId, pageId, isPinned, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{noteId:guid}", Name = nameof(GetNoteById))]
    public async Task<IActionResult> GetNoteById(Guid workspaceId, Guid noteId, CancellationToken cancellationToken)
    {
        var result = await _noteService.GetNoteByIdAsync(workspaceId, noteId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNote(Guid workspaceId, [FromBody] CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _noteService.CreateNoteAsync(workspaceId, request, cancellationToken);
        if (result.IsSuccess)
        {
            return HandleCreatedResult(result, nameof(GetNoteById), new { workspaceId, noteId = result.Value.Id });
        }

        return HandleResult(result);
    }

    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> UpdateNote(Guid workspaceId, Guid noteId, [FromBody] UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _noteService.UpdateNoteAsync(workspaceId, noteId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid workspaceId, Guid noteId, CancellationToken cancellationToken)
    {
        var result = await _noteService.DeleteNoteAsync(workspaceId, noteId, cancellationToken);
        return HandleResult(result);
    }
}
