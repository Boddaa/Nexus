namespace Nexus.Application.DTOs.Conversations;

public record ConversationDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    string Title,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int MessageCount);

public record CreateConversationRequest(
    string? Title = null,
    Nexus.Domain.Enums.ContextType? ContextType = null,
    Guid? ContextEntityId = null);

public record UpdateConversationRequest(
    string Title,
    bool? IsArchived = null);

public record ChatSourceDto(
    Guid Id,
    Guid? DocumentId,
    Guid? DocumentChunkId,
    Guid? PageId,
    Guid? NoteId,
    string Title,
    string SourceType,
    double RelevanceScore,
    int? PageNumber,
    string? Snippet);

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    int? TokenCount,
    IReadOnlyList<ChatSourceDto> Sources);

public record SendChatMessageRequest(
    string Content);

public record SendChatMessageResponse(
    ConversationDto Conversation,
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage,
    IReadOnlyList<ChatSourceDto> Sources);
