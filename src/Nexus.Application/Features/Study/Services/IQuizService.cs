using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public interface IQuizService
{
    Task<Result<IReadOnlyList<QuizDto>>> GetQuizzesAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<QuizDetailDto>> GetQuizByIdAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
    Task<Result<SafeQuizDetailDto>> GetSafeQuizByIdAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
    Task<Result<QuizDetailDto>> GenerateQuizAsync(Guid workspaceId, Guid? topicId, GenerateQuizRequest request, CancellationToken cancellationToken = default);
    Task<Result<QuizAttemptResultDto>> StartQuizAttemptAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
    Task<Result<QuizAttemptResultDto>> SubmitQuizAttemptAsync(Guid workspaceId, Guid attemptId, SubmitQuizAttemptRequest request, CancellationToken cancellationToken = default);
    Task<Result<QuizAttemptResultDto>> GetAttemptResultAsync(Guid workspaceId, Guid attemptId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
}
