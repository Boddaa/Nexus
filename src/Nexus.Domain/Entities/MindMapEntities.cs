using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class MindMap : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? UserId { get; set; }
    public string Title { get; set; } = "Untitled Mind Map";
    public string? Description { get; set; }
    public Guid? RootNodeId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<MindMapNode> Nodes { get; set; } = new List<MindMapNode>();
    public ICollection<MindMapEdge> Edges { get; set; } = new List<MindMapEdge>();

    public MindMap() { }
    public MindMap(Guid id) { Id = id; }
}

public class MindMapNode : AuditableEntity
{
    public Guid MindMapId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string Title { get; set; } = "Concept";
    public string? Description { get; set; }
    public double PositionX { get; set; } = 0.0;
    public double PositionY { get; set; } = 0.0;
    public double Width { get; set; } = 180.0;
    public double Height { get; set; } = 80.0;
    public string ColorHex { get; set; } = "#3B82F6";
    public string Shape { get; set; } = "RoundedRectangle"; // "Circle", "Rectangle", "Pill"
    public MindMapNodeType NodeType { get; set; } = MindMapNodeType.Concept;
    
    // Linked Knowledge Entity (Page, Note, Document, DocumentChunk, StudyTopic, Quiz, Flashcard)
    public string? LinkedEntityType { get; set; }
    public Guid? LinkedEntityId { get; set; }

    public double X
    {
        get => PositionX;
        set => PositionX = value;
    }

    public double Y
    {
        get => PositionY;
        set => PositionY = value;
    }

    public MindMap MindMap { get; set; } = null!;
    public MindMapNode? ParentNode { get; set; }
    public ICollection<MindMapNode> Children { get; set; } = new List<MindMapNode>();
    public ICollection<MindMapEdge> SourceEdges { get; set; } = new List<MindMapEdge>();
    public ICollection<MindMapEdge> TargetEdges { get; set; } = new List<MindMapEdge>();

    public MindMapNode() { }
    public MindMapNode(Guid id) { Id = id; }
}

public class MindMapEdge : AuditableEntity
{
    public Guid MindMapId { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string? Label { get; set; }
    public string? RelationType { get; set; }
    public string Style { get; set; } = "Solid"; // "Dashed", "Solid", "Curved"
    public MindMapEdgeType EdgeType { get; set; } = MindMapEdgeType.RelatesTo;

    public MindMap MindMap { get; set; } = null!;
    public MindMapNode SourceNode { get; set; } = null!;
    public MindMapNode TargetNode { get; set; } = null!;

    public MindMapEdge() { }
    public MindMapEdge(Guid id) { Id = id; }
}
