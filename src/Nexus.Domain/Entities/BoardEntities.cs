using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Board : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "Main Board";
    public string? Description { get; set; }
    public BoardType Type { get; set; } = BoardType.StudyBoard;

    public Workspace Workspace { get; set; } = null!;
    public ICollection<BoardColumn> Columns { get; set; } = new List<BoardColumn>();
}

public class BoardColumn : AuditableEntity
{
    public Guid BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 0;
    public int? WipLimit { get; set; }
    public BoardColumnCategory Category { get; set; } = BoardColumnCategory.Custom;

    public Board Board { get; set; } = null!;
    public ICollection<BoardItem> Items { get; set; } = new List<BoardItem>();
}

public class BoardItem : AuditableEntity
{
    public Guid BoardColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderIndex { get; set; } = 0;
    public ItemPriority Priority { get; set; } = ItemPriority.Medium;
    public DateTime? DueDate { get; set; }
    public string? ColorHex { get; set; }

    // Polymorphic Reference to Note, Document, Task, or Concept
    public string? LinkedEntityType { get; set; } // "Note", "Document", "Task", "Concept"
    public Guid? LinkedEntityId { get; set; }

    public BoardColumn Column { get; set; } = null!;
}
