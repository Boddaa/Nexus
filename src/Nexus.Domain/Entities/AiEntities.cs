using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class AiConversation : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "New Conversation";
    public bool IsArchived { get; set; } = false;
    public ContextType ContextType { get; set; } = ContextType.Workspace;
    public Guid? ContextEntityId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();

    public AiConversation() { }
    public AiConversation(Guid id) { Id = id; }
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

    public AiMessage() { }
    public AiMessage(Guid id) { Id = id; }
}

public class SourceReference : AuditableEntity
{
    public Guid AiMessageId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? DocumentChunkId { get; set; }
    public Guid? PageId { get; set; }
    public Guid? NoteId { get; set; }
    public string SourceType { get; set; } = "Document";
    public string SourceTitle { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double RelevanceScore { get; set; } = 0.0;
    public int PageNumber { get; set; } = 1;

    public AiMessage AiMessage { get; set; } = null!;
    public DocumentChunk? DocumentChunk { get; set; }

    public SourceReference() { }
    public SourceReference(Guid id) { Id = id; }
}

public class AiGeneration : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourcePageId { get; set; }
    public Guid? SourceNoteId { get; set; }
    public Guid? ConversationId { get; set; }
    public string Operation { get; set; } = string.Empty; // "Summarize", "Explain", "KeyPoints", "Questions", "StudyMaterial"
    public string Content { get; set; } = string.Empty;
    public string? StructuredContentJson { get; set; }
    public string Model { get; set; } = string.Empty;

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
    public Document? SourceDocument { get; set; }
    public Page? SourcePage { get; set; }
    public Note? SourceNote { get; set; }
    public AiConversation? Conversation { get; set; }
    public ICollection<AiGenerationSource> Sources { get; set; } = new List<AiGenerationSource>();

    public AiGeneration() { }
    public AiGeneration(Guid id) { Id = id; }
}

public class AiGenerationSource : AuditableEntity
{
    public Guid AiGenerationId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? DocumentChunkId { get; set; }
    public Guid? PageId { get; set; }
    public Guid? NoteId { get; set; }
    public double RelevanceScore { get; set; } = 1.0;
    public string? Title { get; set; }
    public string? Snippet { get; set; }
    public int? PageNumber { get; set; }

    public AiGeneration AiGeneration { get; set; } = null!;
    public Document? Document { get; set; }
    public DocumentChunk? DocumentChunk { get; set; }
    public Page? Page { get; set; }
    public Note? Note { get; set; }

    public AiGenerationSource() { }
    public AiGenerationSource(Guid id) { Id = id; }
}
