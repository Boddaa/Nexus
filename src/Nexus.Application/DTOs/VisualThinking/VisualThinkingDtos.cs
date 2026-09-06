using Nexus.Domain.Enums;

namespace Nexus.Application.DTOs.VisualThinking;

// ==========================================
// Board DTOs
// ==========================================

public record BoardDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? UserId,
    string Title,
    string? Description,
    BoardType Type,
    int ItemCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record BoardDetailDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? UserId,
    string Title,
    string? Description,
    BoardType Type,
    IReadOnlyList<BoardItemDto> Items,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateBoardRequest(
    string Title,
    string? Description = null,
    BoardType Type = BoardType.StudyBoard,
    bool IsPersonal = false
);

public record UpdateBoardRequest(
    string Title,
    string? Description = null,
    BoardType? Type = null
);

// ==========================================
// BoardItem DTOs
// ==========================================

public record BoardItemDto(
    Guid Id,
    Guid BoardId,
    Guid? BoardColumnId,
    BoardItemType Type,
    string Title,
    string? Description,
    string? Content,
    double X,
    double Y,
    double Width,
    double Height,
    double Rotation,
    int ZIndex,
    string? ColorHex,
    string? LinkedEntityType,
    Guid? LinkedEntityId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateBoardItemRequest(
    BoardItemType Type,
    string Title,
    string? Description = null,
    string? Content = null,
    double X = 0,
    double Y = 0,
    double Width = 200,
    double Height = 150,
    double Rotation = 0,
    int ZIndex = 0,
    string? ColorHex = null,
    string? LinkedEntityType = null,
    Guid? LinkedEntityId = null,
    Guid? BoardColumnId = null
);

public record UpdateBoardItemRequest(
    string? Title = null,
    string? Description = null,
    string? Content = null,
    BoardItemType? Type = null,
    double? X = null,
    double? Y = null,
    double? Width = null,
    double? Height = null,
    double? Rotation = null,
    int? ZIndex = null,
    string? ColorHex = null,
    string? LinkedEntityType = null,
    Guid? LinkedEntityId = null,
    Guid? BoardColumnId = null
);

public record BoardItemBatchPositionDto(
    Guid Id,
    double X,
    double Y,
    double? Width = null,
    double? Height = null,
    double? Rotation = null,
    int? ZIndex = null
);

public record BatchUpdateBoardItemsRequest(
    IReadOnlyList<BoardItemBatchPositionDto> Items
);

// ==========================================
// MindMap DTOs
// ==========================================

public record MindMapDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? UserId,
    string Title,
    string? Description,
    Guid? RootNodeId,
    int NodeCount,
    int EdgeCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record MindMapDetailDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? UserId,
    string Title,
    string? Description,
    Guid? RootNodeId,
    IReadOnlyList<MindMapNodeDto> Nodes,
    IReadOnlyList<MindMapEdgeDto> Edges,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateMindMapRequest(
    string Title,
    string? Description = null,
    bool IsPersonal = false
);

public record UpdateMindMapRequest(
    string Title,
    string? Description = null,
    Guid? RootNodeId = null
);

// ==========================================
// MindMap Node DTOs
// ==========================================

public record MindMapNodeDto(
    Guid Id,
    Guid MindMapId,
    Guid? ParentNodeId,
    string Title,
    string? Description,
    double X,
    double Y,
    double Width,
    double Height,
    string ColorHex,
    string Shape,
    MindMapNodeType NodeType,
    string? LinkedEntityType,
    Guid? LinkedEntityId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateMindMapNodeRequest(
    string Title,
    string? Description = null,
    Guid? ParentNodeId = null,
    double X = 0,
    double Y = 0,
    double Width = 180,
    double Height = 80,
    string ColorHex = "#3B82F6",
    string Shape = "RoundedRectangle",
    MindMapNodeType NodeType = MindMapNodeType.Concept,
    string? LinkedEntityType = null,
    Guid? LinkedEntityId = null
);

public record UpdateMindMapNodeRequest(
    string? Title = null,
    string? Description = null,
    Guid? ParentNodeId = null,
    double? X = null,
    double? Y = null,
    double? Width = null,
    double? Height = null,
    string? ColorHex = null,
    string? Shape = null,
    MindMapNodeType? NodeType = null,
    string? LinkedEntityType = null,
    Guid? LinkedEntityId = null
);

public record MindMapNodeBatchPositionDto(
    Guid Id,
    double X,
    double Y,
    double? Width = null,
    double? Height = null
);

public record BatchUpdateMindMapNodesRequest(
    IReadOnlyList<MindMapNodeBatchPositionDto> Nodes
);

// ==========================================
// MindMap Edge DTOs
// ==========================================

public record MindMapEdgeDto(
    Guid Id,
    Guid MindMapId,
    Guid SourceNodeId,
    Guid TargetNodeId,
    string? Label,
    string? RelationType,
    string Style,
    MindMapEdgeType EdgeType,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateMindMapEdgeRequest(
    Guid SourceNodeId,
    Guid TargetNodeId,
    string? Label = null,
    string? RelationType = null,
    string Style = "Solid",
    MindMapEdgeType EdgeType = MindMapEdgeType.RelatesTo
);

public record UpdateMindMapEdgeRequest(
    string? Label = null,
    string? RelationType = null,
    string? Style = null,
    MindMapEdgeType? EdgeType = null
);

// ==========================================
// Auto-Layout DTOs
// ==========================================

public record ApplyLayoutRequest(
    AutoLayoutAlgorithm Algorithm = AutoLayoutAlgorithm.Tree,
    double HorizontalSpacing = 220,
    double VerticalSpacing = 120
);

public record NodePositionDto(
    Guid NodeId,
    double X,
    double Y
);

public record LayoutResultDto(
    IReadOnlyList<NodePositionDto> Positions
);

// ==========================================
// Knowledge Context & Linking DTOs
// ==========================================

public record NodeKnowledgeContextDto(
    Guid NodeId,
    string NodeTitle,
    string EntityType,
    Guid EntityId,
    string EntityTitle,
    string? Snippet,
    string? UrlOrPath = null,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record FindRelatedKnowledgeRequest(
    int Limit = 5
);

public record RelatedKnowledgeItemDto(
    Guid Id,
    string Title,
    string EntityType,
    string Snippet,
    double RelevanceScore
);

public record RelatedKnowledgeResultDto(
    Guid NodeId,
    IReadOnlyList<RelatedKnowledgeItemDto> RelatedItems
);

// ==========================================
// AI Generation & Actions DTOs
// ==========================================

public record GenerateMindMapRequest(
    string Prompt,
    string? SourceType = null, // "Topic", "Document", "Note", "Page", or null
    Guid? SourceId = null,
    int MaxNodes = 15,
    bool IsPersonal = false
);

public record GeneratedMindMapDto(
    Guid MindMapId,
    string Title,
    int NodeCount,
    int EdgeCount,
    Guid? AiGenerationId
);

public record ExplainNodeRequest(
    string? CustomQuestion = null
);

public record NodeExplanationDto(
    Guid NodeId,
    string NodeTitle,
    string Explanation,
    IReadOnlyList<string> KeyTakeaways,
    IReadOnlyList<string> SourceReferences
);
