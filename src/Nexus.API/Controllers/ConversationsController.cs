using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.Features.Conversations.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/conversations")]
public class ConversationsController : ApiControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationsController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    public async Task<IActionResult> ListConversations(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await _conversationService.ListConversationsAsync(workspaceId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation(
        Guid workspaceId,
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _conversationService.CreateConversationAsync(workspaceId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _conversationService.GetConversationAsync(workspaceId, conversationId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _conversationService.GetConversationMessagesAsync(workspaceId, conversationId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid workspaceId,
        Guid conversationId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _conversationService.SendChatMessageAsync(workspaceId, conversationId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("{conversationId:guid}/archive")]
    public async Task<IActionResult> ArchiveConversation(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _conversationService.ArchiveConversationAsync(workspaceId, conversationId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _conversationService.DeleteConversationAsync(workspaceId, conversationId, cancellationToken);
        return HandleResult(result);
    }
}
