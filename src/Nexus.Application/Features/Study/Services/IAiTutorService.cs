using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public interface IAiTutorService
{
    Task<Result<TutorResponseDto>> ChatWithTutorAsync(Guid workspaceId, TutorChatRequest request, CancellationToken cancellationToken = default);
    Task<Result<TutorHintDto>> GetHintAsync(Guid workspaceId, TutorHintRequest request, CancellationToken cancellationToken = default);
    Task<Result<TutorExplanationDto>> ExplainWrongAnswerAsync(Guid workspaceId, TutorExplainWrongAnswerRequest request, CancellationToken cancellationToken = default);
    Task<Result<TutorMiniExerciseDto>> GenerateMiniExerciseAsync(Guid workspaceId, TutorMiniExerciseRequest request, CancellationToken cancellationToken = default);
}
