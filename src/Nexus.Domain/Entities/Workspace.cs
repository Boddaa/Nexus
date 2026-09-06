using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Workspace : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = "📁";
    public string ColorHex { get; set; } = "#3B82F6";
    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Page> Pages { get; set; } = new List<Page>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Board> Boards { get; set; } = new List<Board>();
    public ICollection<MindMap> MindMaps { get; set; } = new List<MindMap>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<KnowledgeConcept> Concepts { get; set; } = new List<KnowledgeConcept>();
    public ICollection<StudyTopic> StudyTopics { get; set; } = new List<StudyTopic>();
    public ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();
}

public class WorkspaceMember : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Editor;

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
}
