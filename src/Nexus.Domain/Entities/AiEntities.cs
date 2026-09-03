using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class AiConversation : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "New Conversation";
    public ContextType ContextType { get; set; } = ContextType.Workspace;
    public Guid? ContextEntityId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}

public class AiMessage : AuditableEntity
{
    public Guid ConversationId { get; set; }
    public AiRole Role { get; set; } = AiRole.User;
    public string Content { get; set; } = string.Empty;
    public int PromptTokens { get; set; } = 0;
    public int CompletionTokens { get; set; } = 0;

    public AiConversation Conversation { get; set; } = null!;
    public ICollection<SourceReference> SourceReferences { get; set; } = new List<SourceReference>();
}

public class SourceReference : AuditableEntity
{
    public Guid AiMessageId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? DocumentChunkId { get; set; }
    public Guid? PageId { get; set; }
    public Guid? NoteId { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double RelevanceScore { get; set; } = 0.0;
    public int PageNumber { get; set; } = 1;

    public AiMessage AiMessage { get; set; } = null!;
    public DocumentChunk? DocumentChunk { get; set; }
}
