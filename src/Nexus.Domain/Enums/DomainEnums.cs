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
