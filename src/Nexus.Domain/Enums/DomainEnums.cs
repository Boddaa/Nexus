namespace Nexus.Domain.Enums;

public enum WorkspaceRole
{
    Owner = 1,
    Editor = 2,
    Viewer = 3
}

public enum DocumentStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4
}

public enum BoardType
{
    Kanban = 1,
    StudyBoard = 2
}

public enum BoardColumnCategory
{
    ToLearn = 1,
    Learning = 2,
    Review = 3,
    Mastered = 4,
    Custom = 5
}

public enum ItemPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum QuestionType
{
    MultipleChoice = 1,
    ShortAnswer = 2,
    TrueFalse = 3
}

public enum FlashcardState
{
    New = 1,
    Learning = 2,
    Review = 3,
    Mastered = 4
}

public enum RelationType
{
    Uses = 1,
    Manages = 2,
    Inherits = 3,
    Generates = 4,
    Extends = 5,
    Mentions = 6,
    DependsOn = 7,
    RelatedTo = 8
}

public enum AiRole
{
    User = 1,
    Assistant = 2,
    System = 3
}

public enum ContextType
{
    Workspace = 1,
    Document = 2,
    Page = 3,
    Study = 4
}

public enum StudySessionStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ReviewRating
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4
}

public enum BoardItemType
{
    StickyNote = 1,
    Text = 2,
    Shape = 3,
    Document = 4,
    Note = 5,
    Page = 6,
    Task = 7,
    MindMap = 8,
    ImagePlaceholder = 9,
    Group = 10
}

public enum MindMapNodeType
{
    Concept = 1,
    Document = 2,
    Note = 3,
    Page = 4,
    StudyTopic = 5,
    Quiz = 6,
    Flashcard = 7,
    Custom = 8
}

public enum MindMapEdgeType
{
    RelatesTo = 1,
    DependsOn = 2,
    Causes = 3,
    Contains = 4,
    Implements = 5,
    References = 6
}

public enum AutoLayoutAlgorithm
{
    Tree = 1,
    HorizontalTree = 2,
    VerticalTree = 3,
    Radial = 4
}
