using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public interface IStudySessionService
{
    Task<Result<IReadOnlyList<StudySessionDto>>> GetSessionsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<StudySessionDto>> GetSessionByIdAsync(Guid workspaceId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<Result<StudySessionDto>> StartSessionAsync(Guid workspaceId, Guid? topicId, StartStudySessionRequest request, CancellationToken cancellationToken = default);
    Task<Result<StudySessionDto>> CompleteSessionAsync(Guid workspaceId, Guid sessionId, CompleteStudySessionRequest request, CancellationToken cancellationToken = default);
}
