using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public interface IFlashcardService
{
    Task<Result<IReadOnlyList<FlashcardDto>>> GetFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FlashcardDto>>> GetDueFlashcardsAsync(Guid workspaceId, Guid? topicId = null, int? limit = null, CancellationToken cancellationToken = default);
    Task<Result<FlashcardDto>> GetFlashcardByIdAsync(Guid workspaceId, Guid cardId, CancellationToken cancellationToken = default);
    Task<Result<FlashcardDto>> CreateFlashcardAsync(Guid workspaceId, Guid? topicId, CreateFlashcardRequest request, CancellationToken cancellationToken = default);
    Task<Result<FlashcardDto>> ReviewFlashcardAsync(Guid workspaceId, Guid cardId, ReviewFlashcardRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FlashcardDto>>> GenerateFlashcardsAsync(Guid workspaceId, Guid? topicId, GenerateFlashcardsRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteFlashcardAsync(Guid workspaceId, Guid cardId, CancellationToken cancellationToken = default);
}
