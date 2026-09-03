using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class StudySession : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "Study Session";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 0;
    public string? Notes { get; set; }

    public Workspace Workspace { get; set; } = null!;
}

public class Flashcard : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? ConceptId { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public double EaseFactor { get; set; } = 2.5; // SM-2 parameter
    public int Repetitions { get; set; } = 0;
    public int IntervalDays { get; set; } = 1;
    public DateTime NextReviewDateUtc { get; set; } = DateTime.UtcNow;
    public FlashcardState State { get; set; } = FlashcardState.New;

    public Workspace Workspace { get; set; } = null!;
    public KnowledgeConcept? Concept { get; set; }
}

public class Quiz : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "Untitled Quiz";
    public string? Description { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string DifficultyLevel { get; set; } = "Medium"; // "Easy", "Medium", "Hard"

    public Workspace Workspace { get; set; } = null!;
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}

public class QuizQuestion : AuditableEntity
{
    public Guid QuizId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
    public string OptionsJson { get; set; } = "[]"; // JSON array of options for MCQ
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; } = 0;

    public Quiz Quiz { get; set; } = null!;
}

public class QuizAttempt : AuditableEntity
{
    public Guid QuizId { get; set; }
    public Guid UserId { get; set; }
    public double ScorePercentage { get; set; } = 0.0;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public string AnswersJson { get; set; } = "[]"; // User selected answers
    public string? AiFeedback { get; set; }

    public Quiz Quiz { get; set; } = null!;
    public User User { get; set; } = null!;
}
