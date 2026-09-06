using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Conversations.Services;

public class ConversationService : IConversationService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRagService _ragService;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IRagService ragService,
        IOptions<RagOptions> ragOptions,
        ILogger<ConversationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _ragService = ragService;
        _ragOptions = ragOptions?.Value ?? new RagOptions();
        _logger = logger;
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return false;

        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId &&
                           !w.IsDeleted &&
                           (w.OwnerId == userId.Value || w.Members.Any(m => m.UserId == userId.Value && !m.IsDeleted)),
                      cancellationToken);
    }

    public async Task<Result<ConversationDto>> CreateConversationAsync(
        Guid workspaceId,
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<ConversationDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<ConversationDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var title = string.IsNullOrWhiteSpace(request?.Title) ? "New Conversation" : request.Title.Trim();

        var conversation = new AiConversation
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            Title = title,
            IsArchived = false,
            ContextType = ContextType.Workspace
        };

        _context.AiConversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConversationDto(
            Id: conversation.Id,
            WorkspaceId: conversation.WorkspaceId,
            UserId: conversation.UserId,
            Title: conversation.Title,
            IsArchived: conversation.IsArchived,
            CreatedAtUtc: conversation.CreatedAtUtc,
            UpdatedAtUtc: conversation.UpdatedAtUtc,
            MessageCount: 0));
    }

    public async Task<Result<ConversationDto>> GetConversationAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<ConversationDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        var conversation = await _context.AiConversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId && c.WorkspaceId == workspaceId && c.UserId == userId.Value && !c.IsDeleted)
            .Select(c => new ConversationDto(
                c.Id,
                c.WorkspaceId,
                c.UserId,
                c.Title,
                c.IsArchived,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.Messages.Count(m => !m.IsDeleted)))
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation == null)
        {
            return Result.Failure<ConversationDto>(new Error("Conversation.NotFound", "Conversation not found."));
        }

        return Result.Success(conversation);
    }

    public async Task<Result<IReadOnlyList<ConversationDto>>> ListConversationsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<ConversationDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ConversationDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var conversations = await _context.AiConversations
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId && c.UserId == userId.Value && !c.IsDeleted)
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .Select(c => new ConversationDto(
                c.Id,
                c.WorkspaceId,
                c.UserId,
                c.Title,
                c.IsArchived,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.Messages.Count(m => !m.IsDeleted)))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ConversationDto>>(conversations);
    }

    public async Task<Result<IReadOnlyList<ChatMessageDto>>> GetConversationMessagesAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<ChatMessageDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        var exists = await _context.AiConversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == conversationId && c.WorkspaceId == workspaceId && c.UserId == userId.Value && !c.IsDeleted, cancellationToken);

        if (!exists)
        {
            return Result.Failure<IReadOnlyList<ChatMessageDto>>(new Error("Conversation.NotFound", "Conversation not found."));
        }

        var messages = await _context.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ConversationId,
                m.Role.ToString(),
                m.Content,
                m.CreatedAtUtc,
                m.PromptTokens + m.CompletionTokens,
                m.SourceReferences.Where(s => !s.IsDeleted).Select(s => new ChatSourceDto(
                    s.Id,
                    s.DocumentId,
                    s.DocumentChunkId,
                    s.PageId,
                    s.NoteId,
                    s.SourceTitle,
                    s.SourceType,
                    s.RelevanceScore,
                    s.PageNumber,
                    s.Snippet)).ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ChatMessageDto>>(messages);
    }

    public async Task<Result<bool>> ArchiveConversationAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<bool>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        var conversation = await _context.AiConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.WorkspaceId == workspaceId && c.UserId == userId.Value && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result.Failure<bool>(new Error("Conversation.NotFound", "Conversation not found."));
        }

        conversation.IsArchived = true;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }

    public async Task<Result<bool>> DeleteConversationAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<bool>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        var conversation = await _context.AiConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.WorkspaceId == workspaceId && c.UserId == userId.Value && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result.Failure<bool>(new Error("Conversation.NotFound", "Conversation not found."));
        }

        conversation.IsDeleted = true;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }

    public async Task<Result<SendChatMessageResponse>> SendChatMessageAsync(
        Guid workspaceId,
        Guid conversationId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SendChatMessageResponse>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (string.IsNullOrWhiteSpace(request?.Content))
        {
            return Result.Failure<SendChatMessageResponse>(new Error("Message.Empty", "Message content cannot be empty."));
        }

        var conversation = await _context.AiConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.WorkspaceId == workspaceId && c.UserId == userId.Value && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result.Failure<SendChatMessageResponse>(new Error("Conversation.NotFound", "Conversation not found."));
        }

        // 1. Store user message
        var userMessage = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiRole.User,
            Content = request.Content.Trim()
        };
        _context.AiMessages.Add(userMessage);
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Fetch bounded conversation history (most recent N messages, in chronological order)
        var historyEntities = await _context.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && m.Id != userMessage.Id && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(_ragOptions.MaxConversationMessages)
            .Select(m => new { m.Role, m.Content, m.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var historyMessages = historyEntities
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new LLMChatMessage(m.Role == AiRole.User ? "user" : "assistant", m.Content))
            .ToList();

        // 3. Execute RAG pipeline
        var ragResult = await _ragService.AnswerQuestionAsync(workspaceId, userMessage.Content, historyMessages, cancellationToken);
        if (!ragResult.IsSuccess)
        {
            _logger.LogError("RAG pipeline failed for message in conversation {ConversationId}: {Error}", conversation.Id, ragResult.Error.Description);
            // User message is safely stored; fail cleanly without creating fake assistant message
            return Result.Failure<SendChatMessageResponse>(ragResult.Error);
        }

        // 4. Store assistant message
        var assistantMessage = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiRole.Assistant,
            Content = ragResult.Value.Answer,
            PromptTokens = ragResult.Value.PromptTokens ?? 0,
            CompletionTokens = ragResult.Value.CompletionTokens ?? 0
        };
        _context.AiMessages.Add(assistantMessage);

        // 5. Store source references
        var sources = ragResult.Value.Sources ?? Array.Empty<ChatSourceDto>();
        foreach (var s in sources)
        {
            var sourceEntity = new SourceReference
            {
                AiMessageId = assistantMessage.Id,
                DocumentId = s.DocumentId,
                DocumentChunkId = s.DocumentChunkId,
                PageId = s.PageId,
                NoteId = s.NoteId,
                SourceTitle = s.Title,
                SourceType = s.SourceType,
                Snippet = s.Snippet ?? string.Empty,
                RelevanceScore = s.RelevanceScore,
                PageNumber = s.PageNumber ?? 1
            };
            _context.SourceReferences.Add(sourceEntity);
        }

        // Auto-generate title if it's the first message or still default
        if (conversation.Title == "New Conversation" && !string.IsNullOrWhiteSpace(userMessage.Content))
        {
            var cleanText = userMessage.Content.Length > 40
                ? userMessage.Content.Substring(0, 37) + "..."
                : userMessage.Content;
            conversation.Title = cleanText;
        }

        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var conversationDto = new ConversationDto(
            conversation.Id,
            conversation.WorkspaceId,
            conversation.UserId,
            conversation.Title,
            conversation.IsArchived,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            await _context.AiMessages.CountAsync(m => m.ConversationId == conversation.Id && !m.IsDeleted, cancellationToken));

        var userMsgDto = new ChatMessageDto(
            userMessage.Id,
            userMessage.ConversationId,
            "User",
            userMessage.Content,
            userMessage.CreatedAtUtc,
            0,
            Array.Empty<ChatSourceDto>());

        var assistantMsgDto = new ChatMessageDto(
            assistantMessage.Id,
            assistantMessage.ConversationId,
            "Assistant",
            assistantMessage.Content,
            assistantMessage.CreatedAtUtc,
            assistantMessage.PromptTokens + assistantMessage.CompletionTokens,
            sources);

        return Result.Success(new SendChatMessageResponse(
            Conversation: conversationDto,
            UserMessage: userMsgDto,
            AssistantMessage: assistantMsgDto,
            Sources: sources));
    }
}
