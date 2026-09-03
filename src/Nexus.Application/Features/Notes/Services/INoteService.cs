using Nexus.Application.DTOs.Notes;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Notes.Services;

public interface INoteService
{
    Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default);
}
