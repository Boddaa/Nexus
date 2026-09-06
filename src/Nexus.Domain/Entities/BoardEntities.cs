using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Board : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? UserId { get; set; }
    public string Title { get; set; } = "Main Board";
    public string? Description { get; set; }
    public BoardType Type { get; set; } = BoardType.StudyBoard;

    public Workspace Workspace { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<BoardColumn> Columns { get; set; } = new List<BoardColumn>();
    public ICollection<BoardItem> Items { get; set; } = new List<BoardItem>();

    public Board() { }
    public Board(Guid id) { Id = id; }
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

    public BoardColumn() { }
    public BoardColumn(Guid id) { Id = id; }
}

public class BoardItem : AuditableEntity
{
    public Guid BoardId { get; set; }
    public Guid? BoardColumnId { get; set; }
    public BoardItemType Type { get; set; } = BoardItemType.StickyNote;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public double X { get; set; } = 0.0;
    public double Y { get; set; } = 0.0;
    public double Width { get; set; } = 200.0;
    public double Height { get; set; } = 150.0;
    public double Rotation { get; set; } = 0.0;
    public int ZIndex { get; set; } = 0;
    public int OrderIndex { get; set; } = 0;
    public ItemPriority Priority { get; set; } = ItemPriority.Medium;
    public DateTime? DueDate { get; set; }
    public string? ColorHex { get; set; }

    // Polymorphic Reference to Note, Document, Task, Concept, Page, StudyTopic, etc.
    public string? LinkedEntityType { get; set; }
    public Guid? LinkedEntityId { get; set; }

    public Board Board { get; set; } = null!;
    public BoardColumn? Column { get; set; }

    public BoardItem() { }
    public BoardItem(Guid id) { Id = id; }
}
