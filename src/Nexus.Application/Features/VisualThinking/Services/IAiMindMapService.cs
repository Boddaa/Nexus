using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.VisualThinking.Services;

public interface IAiMindMapService
{
    Task<Result<GeneratedMindMapDto>> GenerateMindMapAsync(Guid workspaceId, GenerateMindMapRequest request, CancellationToken cancellationToken = default);
    Task<Result<NodeExplanationDto>> ExplainNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, ExplainNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result<RelatedKnowledgeResultDto>> FindRelatedKnowledgeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, FindRelatedKnowledgeRequest request, CancellationToken cancellationToken = default);
}
