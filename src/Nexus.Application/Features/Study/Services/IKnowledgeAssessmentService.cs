using Nexus.Application.DTOs.Study;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Study.Services;

public interface IKnowledgeAssessmentService
{
    Task<Result<TopicPerformanceDto>> GetTopicPerformanceAsync(Guid workspaceId, Guid topicId, CancellationToken cancellationToken = default);
    Task<Result<KnowledgeAssessmentDto>> GetAssessmentAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<StudyDashboardDto>> GetDashboardAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
