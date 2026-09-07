using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.VisualThinking.Services;

public class MindMapService : IMindMapService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAutoLayoutService _autoLayoutService;
    private readonly VisualThinkingOptions _options;
    private readonly ILogger<MindMapService> _logger;

    public MindMapService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IAutoLayoutService autoLayoutService,
        IOptions<VisualThinkingOptions> options,
        ILogger<MindMapService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _autoLayoutService = autoLayoutService;
        _options = options.Value;
        _logger = logger;
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return false;

        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                           (w.OwnerId == userId.Value || w.Members.Any(m => m.UserId == userId.Value && !m.IsDeleted)),
                      cancellationToken);
    }

    private async Task<bool> IsWorkspaceOwnerAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return false;

        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted && w.OwnerId == userId.Value, cancellationToken);
    }

    private async Task<MindMap?> GetValidMindMapAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken, bool tracking = false)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return null;

        var isOwner = await IsWorkspaceOwnerAsync(workspaceId, cancellationToken);
        var query = tracking ? _context.MindMaps : _context.MindMaps.AsNoTracking();

        var mindMap = await query
            .FirstOrDefaultAsync(m => m.Id == mindMapId && m.WorkspaceId == workspaceId && !m.IsDeleted, cancellationToken);

        if (mindMap == null) return null;

        if (mindMap.UserId.HasValue && mindMap.UserId.Value != userId.Value && !isOwner)
        {
            return null;
        }

        return mindMap;
    }

    public async Task<Result<IReadOnlyList<MindMapDto>>> GetMindMapsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<MindMapDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<MindMapDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var isOwner = await IsWorkspaceOwnerAsync(workspaceId, cancellationToken);

        var maps = await _context.MindMaps
            .AsNoTracking()
            .Where(m => m.WorkspaceId == workspaceId && !m.IsDeleted &&
                        (m.UserId == null || m.UserId == userId.Value || isOwner))
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new MindMapDto(
                m.Id,
                m.WorkspaceId,
                m.UserId,
                m.Title,
                m.Description,
                m.RootNodeId,
                m.Nodes.Count(n => !n.IsDeleted),
                m.Edges.Count(e => !e.IsDeleted),
                m.CreatedAtUtc,
                m.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<MindMapDto>>(maps);
    }

    public async Task<Result<MindMapDetailDto>> GetMindMapByIdAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapDetailDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapDetailDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<MindMapDetailDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        // Bounded single query for nodes
        var nodes = await _context.MindMapNodes
            .AsNoTracking()
            .Where(n => n.MindMapId == mindMapId && !n.IsDeleted)
            .Take(_options.MaxNodesPerMindMap)
            .Select(n => new MindMapNodeDto(
                n.Id,
                n.MindMapId,
                n.ParentNodeId,
                n.Title,
                n.Description,
                n.PositionX,
                n.PositionY,
                n.Width,
                n.Height,
                n.ColorHex,
                n.Shape,
                n.NodeType,
                n.LinkedEntityType,
                n.LinkedEntityId,
                n.CreatedAtUtc,
                n.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        // Bounded single query for edges
        var edges = await _context.MindMapEdges
            .AsNoTracking()
            .Where(e => e.MindMapId == mindMapId && !e.IsDeleted)
            .Take(_options.MaxEdgesPerMindMap)
            .Select(e => new MindMapEdgeDto(
                e.Id,
                e.MindMapId,
                e.SourceNodeId,
                e.TargetNodeId,
                e.Label,
                e.RelationType,
                e.Style,
                e.EdgeType,
                e.CreatedAtUtc,
                e.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var dto = new MindMapDetailDto(
            mindMap.Id,
            mindMap.WorkspaceId,
            mindMap.UserId,
            mindMap.Title,
            mindMap.Description,
            mindMap.RootNodeId,
            nodes,
            edges,
            mindMap.CreatedAtUtc,
            mindMap.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<MindMapDto>> CreateMindMapAsync(Guid workspaceId, CreateMindMapRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<MindMapDto>(new Error("MindMap.Validation", "Mind map title is required."));
        }

        if (request.Title.Length > _options.MaxTitleLength)
        {
            return Result.Failure<MindMapDto>(new Error("MindMap.Validation", $"Title cannot exceed {_options.MaxTitleLength} characters."));
        }

        var mapCount = await _context.MindMaps
            .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted, cancellationToken);

        if (mapCount >= _options.MaxMindMapsPerWorkspace)
        {
            return Result.Failure<MindMapDto>(new Error("MindMap.LimitExceeded", $"Maximum number of mind maps ({_options.MaxMindMapsPerWorkspace}) reached for this workspace."));
        }

        var mindMap = new MindMap(Guid.NewGuid())
        {
            WorkspaceId = workspaceId,
            UserId = request.IsPersonal ? userId.Value : null,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim()
        };

        _context.MindMaps.Add(mindMap);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created mind map {MindMapId} in workspace {WorkspaceId}", mindMap.Id, workspaceId);

        var dto = new MindMapDto(
            mindMap.Id,
            mindMap.WorkspaceId,
            mindMap.UserId,
            mindMap.Title,
            mindMap.Description,
            mindMap.RootNodeId,
            0,
            0,
            mindMap.CreatedAtUtc,
            mindMap.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<MindMapDto>> UpdateMindMapAsync(Guid workspaceId, Guid mindMapId, UpdateMindMapRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken, tracking: true);
        if (mindMap == null)
        {
            return Result.Failure<MindMapDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<MindMapDto>(new Error("MindMap.Validation", "Title cannot be empty."));
        }

        if (request.Title.Length > _options.MaxTitleLength)
        {
            return Result.Failure<MindMapDto>(new Error("MindMap.Validation", $"Title cannot exceed {_options.MaxTitleLength} characters."));
        }

        mindMap.Title = request.Title.Trim();
        mindMap.Description = request.Description?.Trim();
        if (request.RootNodeId.HasValue)
        {
            mindMap.RootNodeId = request.RootNodeId.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var nodeCount = await _context.MindMapNodes.CountAsync(n => n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);
        var edgeCount = await _context.MindMapEdges.CountAsync(e => e.MindMapId == mindMapId && !e.IsDeleted, cancellationToken);

        var dto = new MindMapDto(
            mindMap.Id,
            mindMap.WorkspaceId,
            mindMap.UserId,
            mindMap.Title,
            mindMap.Description,
            mindMap.RootNodeId,
            nodeCount,
            edgeCount,
            mindMap.CreatedAtUtc,
            mindMap.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result> DeleteMindMapAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken, tracking: true);
        if (mindMap == null)
        {
            return Result.Failure(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        mindMap.IsDeleted = true;

        var nodes = await _context.MindMapNodes
            .Where(n => n.MindMapId == mindMapId && !n.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var node in nodes) node.IsDeleted = true;

        var edges = await _context.MindMapEdges
            .Where(e => e.MindMapId == mindMapId && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var edge in edges) edge.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted mind map {MindMapId}, {NodeCount} nodes, {EdgeCount} edges", mindMapId, nodes.Count, edges.Count);

        return Result.Success();
    }

    public async Task<Result<MindMapNodeDto>> CreateNodeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapNodeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapNodeDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapNodeDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<MindMapNodeDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<MindMapNodeDto>(new Error("MindMapNode.Validation", "Node title is required."));
        }

        var nodeCount = await _context.MindMapNodes
            .CountAsync(n => n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

        if (nodeCount >= _options.MaxNodesPerMindMap)
        {
            return Result.Failure<MindMapNodeDto>(new Error("MindMapNode.LimitExceeded", $"Maximum number of nodes ({_options.MaxNodesPerMindMap}) reached for this mind map."));
        }

        if (request.ParentNodeId.HasValue)
        {
            var parentExists = await _context.MindMapNodes
                .AnyAsync(n => n.Id == request.ParentNodeId.Value && n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

            if (!parentExists)
            {
                return Result.Failure<MindMapNodeDto>(new Error("MindMapNode.ParentNotFound", "Parent node not found on this mind map."));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.LinkedEntityType) || request.LinkedEntityId.HasValue)
        {
            var linkRes = await ValidateKnowledgeLinkAsync(workspaceId, userId.Value, request.LinkedEntityType, request.LinkedEntityId, cancellationToken);
            if (!linkRes.IsSuccess)
            {
                return Result.Failure<MindMapNodeDto>(linkRes.Error);
            }
        }

        var node = new MindMapNode(Guid.NewGuid())
        {
            MindMapId = mindMapId,
            ParentNodeId = request.ParentNodeId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            PositionX = request.X,
            PositionY = request.Y,
            Width = request.Width > 0 ? request.Width : 180,
            Height = request.Height > 0 ? request.Height : 80,
            ColorHex = !string.IsNullOrWhiteSpace(request.ColorHex) ? request.ColorHex : "#3B82F6",
            Shape = !string.IsNullOrWhiteSpace(request.Shape) ? request.Shape : "RoundedRectangle",
            NodeType = request.NodeType,
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId
        };

        _context.MindMapNodes.Add(node);

        // If root node is not set, set this as root
        if (!mindMap.RootNodeId.HasValue)
        {
            var trackedMap = await _context.MindMaps.FirstOrDefaultAsync(m => m.Id == mindMapId, cancellationToken);
            if (trackedMap != null)
            {
                trackedMap.RootNodeId = node.Id;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new MindMapNodeDto(
            node.Id,
            node.MindMapId,
            node.ParentNodeId,
            node.Title,
            node.Description,
            node.PositionX,
            node.PositionY,
            node.Width,
            node.Height,
            node.ColorHex,
            node.Shape,
            node.NodeType,
            node.LinkedEntityType,
            node.LinkedEntityId,
            node.CreatedAtUtc,
            node.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<MindMapNodeDto>> UpdateNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, UpdateMindMapNodeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapNodeDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapNodeDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<MindMapNodeDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        var node = await _context.MindMapNodes
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

        if (node == null)
        {
            var existsElsewhere = await _context.MindMapNodes
                .AsNoTracking()
                .AnyAsync(n => n.Id == nodeId && !n.IsDeleted, cancellationToken);

            if (existsElsewhere)
            {
                return Result.Failure<MindMapNodeDto>(new Error("MindMapNode.Mismatch", "Node does not belong to the specified mind map."));
            }

            return Result.Failure<MindMapNodeDto>(new Error("MindMapNode.NotFound", "Node not found on this mind map."));
        }

        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Result.Failure<MindMapNodeDto>(new Error("MindMapNode.Validation", "Title cannot be empty."));
            }
            node.Title = request.Title.Trim();
        }

        if (request.Description != null) node.Description = request.Description.Trim();
        if (request.ParentNodeId.HasValue) node.ParentNodeId = request.ParentNodeId.Value;
        if (request.X.HasValue) node.PositionX = request.X.Value;
        if (request.Y.HasValue) node.PositionY = request.Y.Value;
        if (request.Width.HasValue && request.Width.Value > 0) node.Width = request.Width.Value;
        if (request.Height.HasValue && request.Height.Value > 0) node.Height = request.Height.Value;
        if (request.ColorHex != null) node.ColorHex = request.ColorHex;
        if (request.Shape != null) node.Shape = request.Shape;
        if (request.NodeType.HasValue) node.NodeType = request.NodeType.Value;

        if (request.LinkedEntityType != null || request.LinkedEntityId.HasValue)
        {
            var typeToValidate = request.LinkedEntityType ?? node.LinkedEntityType;
            var idToValidate = request.LinkedEntityId ?? node.LinkedEntityId;
            var linkRes = await ValidateKnowledgeLinkAsync(workspaceId, userId.Value, typeToValidate, idToValidate, cancellationToken);
            if (!linkRes.IsSuccess)
            {
                return Result.Failure<MindMapNodeDto>(linkRes.Error);
            }
            if (request.LinkedEntityType != null) node.LinkedEntityType = request.LinkedEntityType;
            if (request.LinkedEntityId.HasValue) node.LinkedEntityId = request.LinkedEntityId.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new MindMapNodeDto(
            node.Id,
            node.MindMapId,
            node.ParentNodeId,
            node.Title,
            node.Description,
            node.PositionX,
            node.PositionY,
            node.Width,
            node.Height,
            node.ColorHex,
            node.Shape,
            node.NodeType,
            node.LinkedEntityType,
            node.LinkedEntityId,
            node.CreatedAtUtc,
            node.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result> DeleteNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        var node = await _context.MindMapNodes
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

        if (node == null)
        {
            return Result.Failure(new Error("MindMapNode.NotFound", "Node not found on this mind map."));
        }

        node.IsDeleted = true;

        // Cascade soft-delete connected edges
        var connectedEdges = await _context.MindMapEdges
            .Where(e => e.MindMapId == mindMapId && (e.SourceNodeId == nodeId || e.TargetNodeId == nodeId) && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var edge in connectedEdges)
        {
            edge.IsDeleted = true;
        }

        // Check if root node deleted
        var trackedMap = await _context.MindMaps.FirstOrDefaultAsync(m => m.Id == mindMapId, cancellationToken);
        if (trackedMap != null && trackedMap.RootNodeId == nodeId)
        {
            var nextRoot = await _context.MindMapNodes
                .Where(n => n.MindMapId == mindMapId && n.Id != nodeId && !n.IsDeleted)
                .Select(n => (Guid?)n.Id)
                .FirstOrDefaultAsync(cancellationToken);

            trackedMap.RootNodeId = nextRoot;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted node {NodeId} and {EdgeCount} connected edges", nodeId, connectedEdges.Count);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MindMapNodeDto>>> BatchUpdateNodesAsync(Guid workspaceId, Guid mindMapId, BatchUpdateMindMapNodesRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        if (request.Nodes == null || request.Nodes.Count == 0)
        {
            return Result.Success<IReadOnlyList<MindMapNodeDto>>(Array.Empty<MindMapNodeDto>());
        }

        if (request.Nodes.Count > _options.MaxBatchNodes)
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("MindMapNode.BatchLimit", $"Batch node update exceeds maximum allowed ({_options.MaxBatchNodes})."));
        }

        var nodeIds = request.Nodes.Select(x => x.Id).ToList();

        var dbNodes = await _context.MindMapNodes
            .Where(n => nodeIds.Contains(n.Id) && !n.IsDeleted)
            .ToListAsync(cancellationToken);

        // Validate all belong to this map
        var foreignNodes = dbNodes.Where(n => n.MindMapId != mindMapId).ToList();
        if (foreignNodes.Count > 0)
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("MindMapNode.Mismatch", "One or more nodes do not belong to the specified mind map."));
        }

        if (dbNodes.Count != nodeIds.Count)
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("MindMapNode.NotFound", "One or more nodes in the batch update were not found on this mind map."));
        }

        var nodesById = dbNodes.ToDictionary(n => n.Id);

        foreach (var update in request.Nodes)
        {
            if (nodesById.TryGetValue(update.Id, out var node))
            {
                node.PositionX = update.X;
                node.PositionY = update.Y;
                if (update.Width.HasValue && update.Width.Value > 0) node.Width = update.Width.Value;
                if (update.Height.HasValue && update.Height.Value > 0) node.Height = update.Height.Value;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dtos = dbNodes.Select(n => new MindMapNodeDto(
            n.Id,
            n.MindMapId,
            n.ParentNodeId,
            n.Title,
            n.Description,
            n.PositionX,
            n.PositionY,
            n.Width,
            n.Height,
            n.ColorHex,
            n.Shape,
            n.NodeType,
            n.LinkedEntityType,
            n.LinkedEntityId,
            n.CreatedAtUtc,
            n.UpdatedAtUtc
        )).ToList();

        return Result.Success<IReadOnlyList<MindMapNodeDto>>(dtos);
    }

    public async Task<Result<MindMapEdgeDto>> CreateEdgeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapEdgeDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        // Reject self-edge
        if (request.SourceNodeId == request.TargetNodeId)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.SelfEdgeProhibited", "Self-edges are not allowed in mind maps."));
        }

        // Graph Integrity: SourceNode and TargetNode must exist, be non-deleted, and belong to THIS mind map
        var sourceNode = await _context.MindMapNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.SourceNodeId && !n.IsDeleted, cancellationToken);

        if (sourceNode == null)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.SourceNotFound", "Source node does not exist or has been deleted."));
        }

        if (sourceNode.MindMapId != mindMapId)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.CrossMapProhibited", "Source node belongs to a different mind map."));
        }

        var targetNode = await _context.MindMapNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.TargetNodeId && !n.IsDeleted, cancellationToken);

        if (targetNode == null)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.TargetNotFound", "Target node does not exist or has been deleted."));
        }

        if (targetNode.MindMapId != mindMapId)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.CrossMapProhibited", "Target node belongs to a different mind map."));
        }

        // Reject duplicate edges
        var edgeExists = await _context.MindMapEdges
            .AnyAsync(e => e.MindMapId == mindMapId &&
                           e.SourceNodeId == request.SourceNodeId &&
                           e.TargetNodeId == request.TargetNodeId &&
                           !e.IsDeleted, cancellationToken);

        if (edgeExists)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.Duplicate", "An edge already exists between these two nodes."));
        }

        // Check edge limits
        var edgeCount = await _context.MindMapEdges
            .CountAsync(e => e.MindMapId == mindMapId && !e.IsDeleted, cancellationToken);

        if (edgeCount >= _options.MaxEdgesPerMindMap)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.LimitExceeded", $"Maximum number of edges ({_options.MaxEdgesPerMindMap}) reached for this mind map."));
        }

        if (request.Label != null && request.Label.Length > _options.MaxLabelLength)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.Validation", $"Edge label cannot exceed {_options.MaxLabelLength} characters."));
        }

        var edge = new MindMapEdge(Guid.NewGuid())
        {
            MindMapId = mindMapId,
            SourceNodeId = request.SourceNodeId,
            TargetNodeId = request.TargetNodeId,
            Label = request.Label?.Trim(),
            RelationType = request.RelationType?.Trim(),
            Style = !string.IsNullOrWhiteSpace(request.Style) ? request.Style : "Solid",
            EdgeType = request.EdgeType
        };

        _context.MindMapEdges.Add(edge);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created edge {EdgeId} between {Source} and {Target} on mind map {MindMapId}", edge.Id, edge.SourceNodeId, edge.TargetNodeId, mindMapId);

        var dto = new MindMapEdgeDto(
            edge.Id,
            edge.MindMapId,
            edge.SourceNodeId,
            edge.TargetNodeId,
            edge.Label,
            edge.RelationType,
            edge.Style,
            edge.EdgeType,
            edge.CreatedAtUtc,
            edge.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<MindMapEdgeDto>> UpdateEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, UpdateMindMapEdgeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<MindMapEdgeDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        var edge = await _context.MindMapEdges
            .FirstOrDefaultAsync(e => e.Id == edgeId && e.MindMapId == mindMapId && !e.IsDeleted, cancellationToken);

        if (edge == null)
        {
            var existsElsewhere = await _context.MindMapEdges
                .AsNoTracking()
                .AnyAsync(e => e.Id == edgeId && !e.IsDeleted, cancellationToken);

            if (existsElsewhere)
            {
                return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.Mismatch", "Edge does not belong to the specified mind map."));
            }

            return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.NotFound", "Edge not found on this mind map."));
        }

        if (request.Label != null)
        {
            if (request.Label.Length > _options.MaxLabelLength)
            {
                return Result.Failure<MindMapEdgeDto>(new Error("MindMapEdge.Validation", $"Edge label cannot exceed {_options.MaxLabelLength} characters."));
            }
            edge.Label = request.Label.Trim();
        }

        if (request.RelationType != null) edge.RelationType = request.RelationType.Trim();
        if (request.Style != null) edge.Style = request.Style.Trim();
        if (request.EdgeType.HasValue) edge.EdgeType = request.EdgeType.Value;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new MindMapEdgeDto(
            edge.Id,
            edge.MindMapId,
            edge.SourceNodeId,
            edge.TargetNodeId,
            edge.Label,
            edge.RelationType,
            edge.Style,
            edge.EdgeType,
            edge.CreatedAtUtc,
            edge.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result> DeleteEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        var edge = await _context.MindMapEdges
            .FirstOrDefaultAsync(e => e.Id == edgeId && e.MindMapId == mindMapId && !e.IsDeleted, cancellationToken);

        if (edge == null)
        {
            return Result.Failure(new Error("MindMapEdge.NotFound", "Edge not found on this mind map."));
        }

        edge.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted edge {EdgeId} on mind map {MindMapId}", edgeId, mindMapId);
        return Result.Success();
    }

    public async Task<Result<LayoutResultDto>> ApplyLayoutAsync(Guid workspaceId, Guid mindMapId, ApplyLayoutRequest request, bool persist = false, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<LayoutResultDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<LayoutResultDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<LayoutResultDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        var nodes = await _context.MindMapNodes
            .Where(n => n.MindMapId == mindMapId && !n.IsDeleted)
            .Take(_options.MaxNodesPerMindMap)
            .Select(n => new MindMapNodeDto(
                n.Id,
                n.MindMapId,
                n.ParentNodeId,
                n.Title,
                n.Description,
                n.PositionX,
                n.PositionY,
                n.Width,
                n.Height,
                n.ColorHex,
                n.Shape,
                n.NodeType,
                n.LinkedEntityType,
                n.LinkedEntityId,
                n.CreatedAtUtc,
                n.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var edges = await _context.MindMapEdges
            .Where(e => e.MindMapId == mindMapId && !e.IsDeleted)
            .Take(_options.MaxEdgesPerMindMap)
            .Select(e => new MindMapEdgeDto(
                e.Id,
                e.MindMapId,
                e.SourceNodeId,
                e.TargetNodeId,
                e.Label,
                e.RelationType,
                e.Style,
                e.EdgeType,
                e.CreatedAtUtc,
                e.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var layoutResult = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, request, cancellationToken);
        if (!layoutResult.IsSuccess)
        {
            return layoutResult;
        }

        if (persist && layoutResult.Value.Positions.Count > 0)
        {
            var positionsByNodeId = layoutResult.Value.Positions.ToDictionary(p => p.NodeId);
            var dbNodes = await _context.MindMapNodes
                .Where(n => n.MindMapId == mindMapId && !n.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var node in dbNodes)
            {
                if (positionsByNodeId.TryGetValue(node.Id, out var pos))
                {
                    node.PositionX = pos.X;
                    node.PositionY = pos.Y;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Persisted auto-layout ({Algorithm}) for mind map {MindMapId} with {NodeCount} nodes", request.Algorithm, mindMapId, dbNodes.Count);
        }

        return layoutResult;
    }

    public async Task<Result<NodeKnowledgeContextDto>> GetNodeKnowledgeContextAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<NodeKnowledgeContextDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<NodeKnowledgeContextDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var mindMap = await GetValidMindMapAsync(workspaceId, mindMapId, cancellationToken);
        if (mindMap == null)
        {
            return Result.Failure<NodeKnowledgeContextDto>(new Error("MindMap.NotFound", "Mind map not found or access denied."));
        }

        var node = await _context.MindMapNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

        if (node == null)
        {
            return Result.Failure<NodeKnowledgeContextDto>(new Error("MindMapNode.NotFound", "Node not found on this mind map."));
        }

        if (string.IsNullOrWhiteSpace(node.LinkedEntityType) || !node.LinkedEntityId.HasValue)
        {
            return Result.Failure<NodeKnowledgeContextDto>(new Error("MindMapNode.NoKnowledgeLink", "This node does not have a linked knowledge entity."));
        }

        var entityType = node.LinkedEntityType.Trim().ToLowerInvariant();
        var entityId = node.LinkedEntityId.Value;

        string entityTitle = string.Empty;
        string? snippet = null;
        string? urlOrPath = null;
        var metadata = new Dictionary<string, string>();

        switch (entityType)
        {
            case "page":
                var page = await _context.Pages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == entityId && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

                if (page == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked Page not found in this workspace."));
                }
                entityTitle = page.Title;
                snippet = page.ContentJson.Length > 300 ? page.ContentJson[..300] + "..." : page.ContentJson;
                metadata["Icon"] = page.Icon;
                break;

            case "note":
                var note = await _context.Notes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == entityId && n.WorkspaceId == workspaceId && !n.IsDeleted, cancellationToken);

                if (note == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked Note not found in this workspace."));
                }
                entityTitle = note.Title;
                snippet = note.Content.Length > 300 ? note.Content[..300] + "..." : note.Content;
                metadata["ContentType"] = note.ContentType;
                break;

            case "document":
                var doc = await _context.Documents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == entityId && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);

                if (doc == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked Document not found in this workspace."));
                }
                entityTitle = doc.Title;
                snippet = doc.Summary ?? (doc.ExtractedText != null && doc.ExtractedText.Length > 300 ? doc.ExtractedText[..300] + "..." : doc.ExtractedText);
                urlOrPath = doc.StoragePath;
                metadata["Status"] = doc.Status.ToString();
                break;

            case "documentchunk":
                var chunk = await _context.DocumentChunks
                    .AsNoTracking()
                    .Include(c => c.Document)
                    .FirstOrDefaultAsync(c => c.Id == entityId && c.Document.WorkspaceId == workspaceId && !c.Document.IsDeleted, cancellationToken);

                if (chunk == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked DocumentChunk not found in this workspace."));
                }
                entityTitle = $"{chunk.Document.Title} (Chunk {chunk.ChunkIndex})";
                snippet = chunk.Text.Length > 300 ? chunk.Text[..300] + "..." : chunk.Text;
                metadata["DocumentId"] = chunk.DocumentId.ToString();
                metadata["ChunkIndex"] = chunk.ChunkIndex.ToString();
                break;

            case "studytopic":
                var topic = await _context.StudyTopics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == entityId && t.WorkspaceId == workspaceId && t.UserId == userId.Value && !t.IsDeleted, cancellationToken);

                if (topic == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked Study Topic not found in this workspace or access denied."));
                }
                entityTitle = topic.Title;
                snippet = topic.Description;
                metadata["TopicId"] = topic.Id.ToString();
                break;

            case "quiz":
                var quiz = await _context.Quizzes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == entityId && q.WorkspaceId == workspaceId && q.UserId == userId.Value && !q.IsDeleted, cancellationToken);

                if (quiz == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked Quiz not found in this workspace or access denied."));
                }
                entityTitle = quiz.Title;
                snippet = quiz.Description;
                metadata["QuizId"] = quiz.Id.ToString();
                break;

            case "flashcard":
                var card = await _context.Flashcards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == entityId && f.WorkspaceId == workspaceId && f.UserId == userId.Value && !f.IsDeleted, cancellationToken);

                if (card == null)
                {
                    return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.NotFound", "Linked Flashcard not found in this workspace or access denied."));
                }
                entityTitle = card.FrontText;
                snippet = card.BackText;
                metadata["CardId"] = card.Id.ToString();
                break;

            default:
                return Result.Failure<NodeKnowledgeContextDto>(new Error("KnowledgeLink.Unsupported", $"Entity type '{node.LinkedEntityType}' is not supported for knowledge context."));
        }

        var contextDto = new NodeKnowledgeContextDto(
            node.Id,
            node.Title,
            node.LinkedEntityType,
            entityId,
            entityTitle,
            snippet,
            urlOrPath,
            metadata
        );

        return Result.Success(contextDto);
    }

    private async Task<Result> ValidateKnowledgeLinkAsync(Guid workspaceId, Guid userId, string? entityType, Guid? entityId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entityType) && !entityId.HasValue)
        {
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(entityType) || !entityId.HasValue)
        {
            return Result.Failure(new Error("KnowledgeLink.Invalid", "Both entity type and entity ID must be specified for a knowledge link."));
        }

        var normalizedType = entityType.Trim().ToLowerInvariant();
        bool exists;

        switch (normalizedType)
        {
            case "page":
                exists = await _context.Pages.AsNoTracking().AnyAsync(p => p.Id == entityId.Value && p.WorkspaceId == workspaceId && !p.IsDeleted, ct);
                break;
            case "note":
                exists = await _context.Notes.AsNoTracking().AnyAsync(n => n.Id == entityId.Value && n.WorkspaceId == workspaceId && !n.IsDeleted, ct);
                break;
            case "document":
                exists = await _context.Documents.AsNoTracking().AnyAsync(d => d.Id == entityId.Value && d.WorkspaceId == workspaceId && !d.IsDeleted, ct);
                break;
            case "documentchunk":
                exists = await _context.DocumentChunks.AsNoTracking().Include(c => c.Document).AnyAsync(c => c.Id == entityId.Value && c.Document.WorkspaceId == workspaceId && !c.Document.IsDeleted, ct);
                break;
            case "studytopic":
                exists = await _context.StudyTopics.AsNoTracking().AnyAsync(t => t.Id == entityId.Value && t.WorkspaceId == workspaceId && t.UserId == userId && !t.IsDeleted, ct);
                break;
            case "quiz":
                exists = await _context.Quizzes.AsNoTracking().AnyAsync(q => q.Id == entityId.Value && q.WorkspaceId == workspaceId && q.UserId == userId && !q.IsDeleted, ct);
                break;
            case "flashcard":
                exists = await _context.Flashcards.AsNoTracking().AnyAsync(f => f.Id == entityId.Value && f.WorkspaceId == workspaceId && f.UserId == userId && !f.IsDeleted, ct);
                break;
            default:
                return Result.Failure(new Error("KnowledgeLink.Unsupported", $"Entity type '{entityType}' is not supported for knowledge links."));
        }

        if (!exists)
        {
            return Result.Failure(new Error("KnowledgeLink.NotFound", $"Linked {entityType} not found in this workspace or access denied."));
        }

        return Result.Success();
    }
}
