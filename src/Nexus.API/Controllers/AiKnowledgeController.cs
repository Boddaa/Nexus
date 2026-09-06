using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.AI;
using Nexus.Application.Features.AI.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/ai")]
public class AiKnowledgeController : ApiControllerBase
{
    private readonly IAiKnowledgeService _aiKnowledgeService;

    public AiKnowledgeController(IAiKnowledgeService aiKnowledgeService)
    {
        _aiKnowledgeService = aiKnowledgeService;
    }

    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize(
        Guid workspaceId,
        [FromBody] AiKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiKnowledgeService.SummarizeAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("explain")]
    public async Task<IActionResult> Explain(
        Guid workspaceId,
        [FromBody] AiKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiKnowledgeService.ExplainAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("key-points")]
    public async Task<IActionResult> KeyPoints(
        Guid workspaceId,
        [FromBody] AiKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiKnowledgeService.ExtractKeyPointsAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("questions")]
    public async Task<IActionResult> Questions(
        Guid workspaceId,
        [FromBody] GenerateQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiKnowledgeService.GenerateQuestionsAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("study-material")]
    public async Task<IActionResult> StudyMaterial(
        Guid workspaceId,
        [FromBody] GenerateStudyMaterialRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiKnowledgeService.GenerateStudyMaterialAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("save-as-note")]
    public async Task<IActionResult> SaveAsNote(
        Guid workspaceId,
        [FromBody] SaveAiOutputAsNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _aiKnowledgeService.SaveAsNoteAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("generations")]
    public async Task<IActionResult> GetGenerations(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _aiKnowledgeService.GetGenerationsAsync(workspaceId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("generations/{generationId:guid}")]
    public async Task<IActionResult> GetGeneration(
        Guid workspaceId,
        Guid generationId,
        CancellationToken cancellationToken)
    {
        var result = await _aiKnowledgeService.GetGenerationAsync(workspaceId, generationId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("generations/{generationId:guid}")]
    public async Task<IActionResult> DeleteGeneration(
        Guid workspaceId,
        Guid generationId,
        CancellationToken cancellationToken)
    {
        var result = await _aiKnowledgeService.DeleteGenerationAsync(workspaceId, generationId, cancellationToken);
        return HandleResult(result);
    }
}
