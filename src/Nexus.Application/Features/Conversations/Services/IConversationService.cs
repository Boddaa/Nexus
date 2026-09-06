using Nexus.Application.DTOs.Conversations;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.Conversations.Services;

public interface IConversationService
{
    Task<Result<ConversationDto>> CreateConversationAsync(
        Guid workspaceId,
        CreateConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationDto>> GetConversationAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ConversationDto>>> ListConversationsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ChatMessageDto>>> GetConversationMessagesAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ArchiveConversationAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteConversationAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<SendChatMessageResponse>> SendChatMessageAsync(
        Guid workspaceId,
        Guid conversationId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ChatMessageDto>> AppendMessageAsync(
        Guid workspaceId,
        Guid conversationId,
        string role,
        string content,
        IReadOnlyList<ChatSourceDto>? sources = null,
        int? tokenCount = null,
        CancellationToken cancellationToken = default);
}
