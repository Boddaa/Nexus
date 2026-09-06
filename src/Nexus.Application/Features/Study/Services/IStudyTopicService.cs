using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public interface IStudyTopicService
{
    Task<Result<IReadOnlyList<StudyTopicDto>>> GetTopicsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<StudyTopicDto>> GetTopicByIdAsync(Guid workspaceId, Guid topicId, CancellationToken cancellationToken = default);
    Task<Result<StudyTopicDto>> CreateTopicAsync(Guid workspaceId, CreateStudyTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<StudyTopicDto>> UpdateTopicAsync(Guid workspaceId, Guid topicId, UpdateStudyTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteTopicAsync(Guid workspaceId, Guid topicId, CancellationToken cancellationToken = default);
}
