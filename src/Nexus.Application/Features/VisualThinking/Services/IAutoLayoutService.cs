using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.VisualThinking.Services;

public interface IAutoLayoutService
{
    Task<Result<LayoutResultDto>> ApplyLayoutAsync(
        IReadOnlyList<MindMapNodeDto> nodes,
        IReadOnlyList<MindMapEdgeDto> edges,
        ApplyLayoutRequest request,
        CancellationToken cancellationToken = default);
}
