using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.VisualThinking.Services;

public class AutoLayoutService : IAutoLayoutService
{
    public Task<Result<LayoutResultDto>> ApplyLayoutAsync(
        IReadOnlyList<MindMapNodeDto> nodes,
        IReadOnlyList<MindMapEdgeDto> edges,
        ApplyLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (nodes == null || nodes.Count == 0)
        {
            return Task.FromResult(Result.Success(new LayoutResultDto(Array.Empty<NodePositionDto>())));
        }

        var positions = request.Algorithm switch
        {
            AutoLayoutAlgorithm.HorizontalTree => CalculateHorizontalTree(nodes, edges, request.HorizontalSpacing, request.VerticalSpacing),
            AutoLayoutAlgorithm.Radial => CalculateRadial(nodes, edges, request.HorizontalSpacing),
            AutoLayoutAlgorithm.Tree or AutoLayoutAlgorithm.VerticalTree or _ => CalculateVerticalTree(nodes, edges, request.HorizontalSpacing, request.VerticalSpacing)
        };

        return Task.FromResult(Result.Success(new LayoutResultDto(positions)));
    }

    private static IReadOnlyList<NodePositionDto> CalculateVerticalTree(
        IReadOnlyList<MindMapNodeDto> nodes,
        IReadOnlyList<MindMapEdgeDto> edges,
        double horizontalSpacing,
        double verticalSpacing)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var inDegrees = nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = nodes.ToDictionary(n => n.Id, _ => new List<Guid>());

        foreach (var edge in edges)
        {
            if (nodeMap.ContainsKey(edge.SourceNodeId) && nodeMap.ContainsKey(edge.TargetNodeId))
            {
                adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
                inDegrees[edge.TargetNodeId]++;
            }
        }

        // Roots: nodes with in-degree 0, ordered deterministically by Id
        var rootCandidates = nodes.Where(n => inDegrees[n.Id] == 0).OrderBy(n => n.Id).Select(n => n.Id).ToList();
        if (rootCandidates.Count == 0 && nodes.Count > 0)
        {
            // Cycle fallback: pick lowest Id
            rootCandidates.Add(nodes.OrderBy(n => n.Id).First().Id);
        }

        var levels = new Dictionary<int, List<Guid>>();
        var visited = new HashSet<Guid>();

        foreach (var rootId in rootCandidates)
        {
            if (visited.Contains(rootId)) continue;

            var queue = new Queue<(Guid NodeId, int Level)>();
            queue.Enqueue((rootId, 0));
            visited.Add(rootId);

            while (queue.Count > 0)
            {
                var (currentId, level) = queue.Dequeue();

                if (!levels.ContainsKey(level))
                {
                    levels[level] = new List<Guid>();
                }
                levels[level].Add(currentId);

                var children = adjacency[currentId].OrderBy(id => id);
                foreach (var childId in children)
                {
                    if (visited.Add(childId))
                    {
                        queue.Enqueue((childId, level + 1));
                    }
                }
            }
        }

        // Remaining unvisited nodes (isolated)
        var remaining = nodes.Where(n => !visited.Contains(n.Id)).OrderBy(n => n.Id).ToList();
        if (remaining.Count > 0)
        {
            var maxLevel = levels.Keys.Count > 0 ? levels.Keys.Max() + 1 : 0;
            levels[maxLevel] = remaining.Select(r => r.Id).ToList();
        }

        var positions = new List<NodePositionDto>();
        const double startX = 400.0;
        const double startY = 100.0;

        foreach (var (level, nodeIds) in levels.OrderBy(kv => kv.Key))
        {
            var totalWidth = (nodeIds.Count - 1) * horizontalSpacing;
            var levelStartX = startX - (totalWidth / 2.0);
            var y = startY + (level * verticalSpacing);

            for (var i = 0; i < nodeIds.Count; i++)
            {
                var x = levelStartX + (i * horizontalSpacing);
                positions.Add(new NodePositionDto(nodeIds[i], Math.Round(x, 2), Math.Round(y, 2)));
            }
        }

        return positions;
    }

    private static IReadOnlyList<NodePositionDto> CalculateHorizontalTree(
        IReadOnlyList<MindMapNodeDto> nodes,
        IReadOnlyList<MindMapEdgeDto> edges,
        double horizontalSpacing,
        double verticalSpacing)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var inDegrees = nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = nodes.ToDictionary(n => n.Id, _ => new List<Guid>());

        foreach (var edge in edges)
        {
            if (nodeMap.ContainsKey(edge.SourceNodeId) && nodeMap.ContainsKey(edge.TargetNodeId))
            {
                adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
                inDegrees[edge.TargetNodeId]++;
            }
        }

        var rootCandidates = nodes.Where(n => inDegrees[n.Id] == 0).OrderBy(n => n.Id).Select(n => n.Id).ToList();
        if (rootCandidates.Count == 0 && nodes.Count > 0)
        {
            rootCandidates.Add(nodes.OrderBy(n => n.Id).First().Id);
        }

        var levels = new Dictionary<int, List<Guid>>();
        var visited = new HashSet<Guid>();

        foreach (var rootId in rootCandidates)
        {
            if (visited.Contains(rootId)) continue;

            var queue = new Queue<(Guid NodeId, int Level)>();
            queue.Enqueue((rootId, 0));
            visited.Add(rootId);

            while (queue.Count > 0)
            {
                var (currentId, level) = queue.Dequeue();

                if (!levels.ContainsKey(level))
                {
                    levels[level] = new List<Guid>();
                }
                levels[level].Add(currentId);

                var children = adjacency[currentId].OrderBy(id => id);
                foreach (var childId in children)
                {
                    if (visited.Add(childId))
                    {
                        queue.Enqueue((childId, level + 1));
                    }
                }
            }
        }

        var remaining = nodes.Where(n => !visited.Contains(n.Id)).OrderBy(n => n.Id).ToList();
        if (remaining.Count > 0)
        {
            var maxLevel = levels.Keys.Count > 0 ? levels.Keys.Max() + 1 : 0;
            levels[maxLevel] = remaining.Select(r => r.Id).ToList();
        }

        var positions = new List<NodePositionDto>();
        const double startX = 100.0;
        const double startY = 300.0;

        foreach (var (level, nodeIds) in levels.OrderBy(kv => kv.Key))
        {
            var totalHeight = (nodeIds.Count - 1) * verticalSpacing;
            var levelStartY = startY - (totalHeight / 2.0);
            var x = startX + (level * horizontalSpacing);

            for (var i = 0; i < nodeIds.Count; i++)
            {
                var y = levelStartY + (i * verticalSpacing);
                positions.Add(new NodePositionDto(nodeIds[i], Math.Round(x, 2), Math.Round(y, 2)));
            }
        }

        return positions;
    }

    private static IReadOnlyList<NodePositionDto> CalculateRadial(
        IReadOnlyList<MindMapNodeDto> nodes,
        IReadOnlyList<MindMapEdgeDto> edges,
        double radialStep)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var inDegrees = nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = nodes.ToDictionary(n => n.Id, _ => new List<Guid>());

        foreach (var edge in edges)
        {
            if (nodeMap.ContainsKey(edge.SourceNodeId) && nodeMap.ContainsKey(edge.TargetNodeId))
            {
                adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
                inDegrees[edge.TargetNodeId]++;
            }
        }

        var rootCandidates = nodes.Where(n => inDegrees[n.Id] == 0).OrderBy(n => n.Id).Select(n => n.Id).ToList();
        var primaryRoot = rootCandidates.Count > 0
            ? rootCandidates.First()
            : nodes.OrderBy(n => n.Id).First().Id;

        const double centerX = 500.0;
        const double centerY = 500.0;

        var positions = new List<NodePositionDto>
        {
            new NodePositionDto(primaryRoot, centerX, centerY)
        };

        var levels = new Dictionary<int, List<Guid>>();
        var visited = new HashSet<Guid> { primaryRoot };

        var queue = new Queue<(Guid NodeId, int Level)>();
        queue.Enqueue((primaryRoot, 0));

        while (queue.Count > 0)
        {
            var (currentId, level) = queue.Dequeue();

            var children = adjacency[currentId].OrderBy(id => id);
            foreach (var childId in children)
            {
                if (visited.Add(childId))
                {
                    var childLevel = level + 1;
                    if (!levels.ContainsKey(childLevel))
                    {
                        levels[childLevel] = new List<Guid>();
                    }
                    levels[childLevel].Add(childId);
                    queue.Enqueue((childId, childLevel));
                }
            }
        }

        // Remaining unvisited nodes placed at outer ring
        var remaining = nodes.Where(n => !visited.Contains(n.Id)).OrderBy(n => n.Id).ToList();
        if (remaining.Count > 0)
        {
            var maxLevel = levels.Keys.Count > 0 ? levels.Keys.Max() + 1 : 1;
            if (!levels.ContainsKey(maxLevel)) levels[maxLevel] = new List<Guid>();
            levels[maxLevel].AddRange(remaining.Select(r => r.Id));
        }

        foreach (var (level, nodeIds) in levels.OrderBy(kv => kv.Key))
        {
            var radius = level * (radialStep > 50 ? radialStep : 180.0);
            var count = nodeIds.Count;
            var angleStep = (2.0 * Math.PI) / count;

            for (var i = 0; i < count; i++)
            {
                var angle = i * angleStep;
                var x = centerX + (radius * Math.Cos(angle));
                var y = centerY + (radius * Math.Sin(angle));
                positions.Add(new NodePositionDto(nodeIds[i], Math.Round(x, 2), Math.Round(y, 2)));
            }
        }

        return positions;
    }
}
