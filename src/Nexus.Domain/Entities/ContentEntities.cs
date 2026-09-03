using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Page : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? ParentPageId { get; set; }
    public string Title { get; set; } = "Untitled";
    public string Icon { get; set; } = "📄";
    public string? CoverImageUrl { get; set; }
    public string ContentJson { get; set; } = "{}";
    public int OrderIndex { get; set; } = 0;

    public Workspace Workspace { get; set; } = null!;
    public Page? ParentPage { get; set; }
    public ICollection<Page> SubPages { get; set; } = new List<Page>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

public class Note : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? PageId { get; set; }
    public string Title { get; set; } = "Untitled Note";
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "markdown"; // markdown or html/rich
    public bool IsPinned { get; set; } = false;

    public Workspace Workspace { get; set; } = null!;
    public Page? Page { get; set; }
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

public class Tag : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#6B7280";

    public Workspace Workspace { get; set; } = null!;
    public ICollection<Page> Pages { get; set; } = new List<Page>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}

public class TaskItem : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
    public ItemPriority Priority { get; set; } = ItemPriority.Medium;

    public Workspace Workspace { get; set; } = null!;
}
