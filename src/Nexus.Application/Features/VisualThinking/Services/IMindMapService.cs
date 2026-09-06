using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.VisualThinking.Services;

public interface IMindMapService
{
    Task<Result<IReadOnlyList<MindMapDto>>> GetMindMapsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<MindMapDetailDto>> GetMindMapByIdAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default);
    Task<Result<MindMapDto>> CreateMindMapAsync(Guid workspaceId, CreateMindMapRequest request, CancellationToken cancellationToken = default);
    Task<Result<MindMapDto>> UpdateMindMapAsync(Guid workspaceId, Guid mindMapId, UpdateMindMapRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteMindMapAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default);

    Task<Result<MindMapNodeDto>> CreateNodeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result<MindMapNodeDto>> UpdateNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, UpdateMindMapNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MindMapNodeDto>>> BatchUpdateNodesAsync(Guid workspaceId, Guid mindMapId, BatchUpdateMindMapNodesRequest request, CancellationToken cancellationToken = default);

    Task<Result<MindMapEdgeDto>> CreateEdgeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapEdgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<MindMapEdgeDto>> UpdateEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, UpdateMindMapEdgeRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, CancellationToken cancellationToken = default);

    Task<Result<LayoutResultDto>> ApplyLayoutAsync(Guid workspaceId, Guid mindMapId, ApplyLayoutRequest request, bool persist = false, CancellationToken cancellationToken = default);
    Task<Result<NodeKnowledgeContextDto>> GetNodeKnowledgeContextAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default);
}
