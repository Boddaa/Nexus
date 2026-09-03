using Nexus.Domain.Common;

namespace Nexus.Domain.Entities;

public class MindMap : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "Untitled Mind Map";
    public string? Description { get; set; }
    public Guid? RootNodeId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<MindMapNode> Nodes { get; set; } = new List<MindMapNode>();
    public ICollection<MindMapEdge> Edges { get; set; } = new List<MindMapEdge>();
}

public class MindMapNode : AuditableEntity
{
    public Guid MindMapId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string Title { get; set; } = "Concept";
    public string? Description { get; set; }
    public double PositionX { get; set; } = 0.0;
    public double PositionY { get; set; } = 0.0;
    public string ColorHex { get; set; } = "#3B82F6";
    public string Shape { get; set; } = "RoundedRectangle"; // "Circle", "Rectangle", "Pill"
    
    // Linked Knowledge Entity
    public string? LinkedEntityType { get; set; } // "Note", "Document", "Concept", "Task"
    public Guid? LinkedEntityId { get; set; }

    public MindMap MindMap { get; set; } = null!;
    public MindMapNode? ParentNode { get; set; }
    public ICollection<MindMapNode> Children { get; set; } = new List<MindMapNode>();
    public ICollection<MindMapEdge> SourceEdges { get; set; } = new List<MindMapEdge>();
    public ICollection<MindMapEdge> TargetEdges { get; set; } = new List<MindMapEdge>();
}

public class MindMapEdge : AuditableEntity
{
    public Guid MindMapId { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string? Label { get; set; }
    public string? RelationType { get; set; }
    public string Style { get; set; } = "Solid"; // "Dashed", "Solid", "Curved"

    public MindMap MindMap { get; set; } = null!;
    public MindMapNode SourceNode { get; set; } = null!;
    public MindMapNode TargetNode { get; set; } = null!;
}
