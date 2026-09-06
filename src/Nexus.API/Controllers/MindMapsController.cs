using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Application.Features.VisualThinking.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/mindmaps")]
public class MindMapsController : ApiControllerBase
{
    private readonly IMindMapService _mindMapService;
    private readonly IAiMindMapService _aiMindMapService;

    public MindMapsController(
        IMindMapService mindMapService,
        IAiMindMapService aiMindMapService)
    {
        _mindMapService = mindMapService;
        _aiMindMapService = aiMindMapService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMindMaps(Guid workspaceId, CancellationToken ct)
    {
        var result = await _mindMapService.GetMindMapsAsync(workspaceId, ct);
        return HandleResult(result);
    }

    [HttpGet("{mindMapId:guid}")]
    public async Task<IActionResult> GetMindMapById(Guid workspaceId, Guid mindMapId, CancellationToken ct)
    {
        var result = await _mindMapService.GetMindMapByIdAsync(workspaceId, mindMapId, ct);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMindMap(Guid workspaceId, [FromBody] CreateMindMapRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.CreateMindMapAsync(workspaceId, request, ct);
        return HandleCreatedResult(result, nameof(GetMindMapById), new { workspaceId, mindMapId = result.Value?.Id });
    }

    [HttpPut("{mindMapId:guid}")]
    public async Task<IActionResult> UpdateMindMap(Guid workspaceId, Guid mindMapId, [FromBody] UpdateMindMapRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.UpdateMindMapAsync(workspaceId, mindMapId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{mindMapId:guid}")]
    public async Task<IActionResult> DeleteMindMap(Guid workspaceId, Guid mindMapId, CancellationToken ct)
    {
        var result = await _mindMapService.DeleteMindMapAsync(workspaceId, mindMapId, ct);
        return HandleResult(result);
    }

    // ==================== Nodes ====================

    [HttpPost("{mindMapId:guid}/nodes")]
    public async Task<IActionResult> CreateNode(Guid workspaceId, Guid mindMapId, [FromBody] CreateMindMapNodeRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.CreateNodeAsync(workspaceId, mindMapId, request, ct);
        return HandleResult(result);
    }

    [HttpPut("{mindMapId:guid}/nodes/{nodeId:guid}")]
    public async Task<IActionResult> UpdateNode(Guid workspaceId, Guid mindMapId, Guid nodeId, [FromBody] UpdateMindMapNodeRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.UpdateNodeAsync(workspaceId, mindMapId, nodeId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{mindMapId:guid}/nodes/{nodeId:guid}")]
    public async Task<IActionResult> DeleteNode(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken ct)
    {
        var result = await _mindMapService.DeleteNodeAsync(workspaceId, mindMapId, nodeId, ct);
        return HandleResult(result);
    }

    [HttpPatch("{mindMapId:guid}/nodes/batch")]
    public async Task<IActionResult> BatchUpdateNodes(Guid workspaceId, Guid mindMapId, [FromBody] BatchUpdateMindMapNodesRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.BatchUpdateNodesAsync(workspaceId, mindMapId, request, ct);
        return HandleResult(result);
    }

    // ==================== Edges ====================

    [HttpPost("{mindMapId:guid}/edges")]
    public async Task<IActionResult> CreateEdge(Guid workspaceId, Guid mindMapId, [FromBody] CreateMindMapEdgeRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.CreateEdgeAsync(workspaceId, mindMapId, request, ct);
        return HandleResult(result);
    }

    [HttpPut("{mindMapId:guid}/edges/{edgeId:guid}")]
    public async Task<IActionResult> UpdateEdge(Guid workspaceId, Guid mindMapId, Guid edgeId, [FromBody] UpdateMindMapEdgeRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _mindMapService.UpdateEdgeAsync(workspaceId, mindMapId, edgeId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{mindMapId:guid}/edges/{edgeId:guid}")]
    public async Task<IActionResult> DeleteEdge(Guid workspaceId, Guid mindMapId, Guid edgeId, CancellationToken ct)
    {
        var result = await _mindMapService.DeleteEdgeAsync(workspaceId, mindMapId, edgeId, ct);
        return HandleResult(result);
    }

    // ==================== Auto-Layout ====================

    [HttpPost("{mindMapId:guid}/layout")]
    public async Task<IActionResult> ApplyLayout(Guid workspaceId, Guid mindMapId, [FromBody] ApplyLayoutRequest request, [FromQuery] bool persist = false, CancellationToken ct = default)
    {
        if (request == null) request = new ApplyLayoutRequest();
        var result = await _mindMapService.ApplyLayoutAsync(workspaceId, mindMapId, request, persist, ct);
        return HandleResult(result);
    }

    // ==================== Knowledge Context ====================

    [HttpGet("{mindMapId:guid}/nodes/{nodeId:guid}/knowledge")]
    public async Task<IActionResult> GetNodeKnowledgeContext(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken ct)
    {
        var result = await _mindMapService.GetNodeKnowledgeContextAsync(workspaceId, mindMapId, nodeId, ct);
        return HandleResult(result);
    }

    // ==================== AI Mind Map & Actions ====================

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateMindMap(Guid workspaceId, [FromBody] GenerateMindMapRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiMindMapService.GenerateMindMapAsync(workspaceId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("{mindMapId:guid}/nodes/{nodeId:guid}/explain")]
    public async Task<IActionResult> ExplainNode(Guid workspaceId, Guid mindMapId, Guid nodeId, [FromBody] ExplainNodeRequest request, CancellationToken ct)
    {
        if (request == null) request = new ExplainNodeRequest();
        var result = await _aiMindMapService.ExplainNodeAsync(workspaceId, mindMapId, nodeId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("{mindMapId:guid}/nodes/{nodeId:guid}/related-knowledge")]
    public async Task<IActionResult> FindRelatedKnowledge(Guid workspaceId, Guid mindMapId, Guid nodeId, [FromBody] FindRelatedKnowledgeRequest request, CancellationToken ct)
    {
        if (request == null) request = new FindRelatedKnowledgeRequest();
        var result = await _aiMindMapService.FindRelatedKnowledgeAsync(workspaceId, mindMapId, nodeId, request, ct);
        return HandleResult(result);
    }
}
