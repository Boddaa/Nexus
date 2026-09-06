using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class StudyTopic : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourcePageId { get; set; }
    public Guid? SourceNoteId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
    public Document? SourceDocument { get; set; }
    public Page? SourcePage { get; set; }
    public Note? SourceNote { get; set; }

    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();

    public StudyTopic() { }
    public StudyTopic(Guid id) { Id = id; }
}

public class StudySession : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid? StudyTopicId { get; set; }
    public string Title { get; set; } = "Study Session";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 0;
    public StudySessionStatus Status { get; set; } = StudySessionStatus.InProgress;
    public int ItemsAttempted { get; set; } = 0;
    public int ItemsCompleted { get; set; } = 0;
    public string? Notes { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User? User { get; set; }
    public StudyTopic? Topic { get; set; }

    public StudySession() { }
    public StudySession(Guid id) { Id = id; }
}

public class Flashcard : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid? StudyTopicId { get; set; }
    public Guid? ConceptId { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public double EaseFactor { get; set; } = 2.5; // SM-2 parameter
    public int Repetitions { get; set; } = 0;
    public int IntervalDays { get; set; } = 1;
    public DateTime NextReviewDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? NextReviewAtUtc
    {
        get => NextReviewDateUtc;
        set { if (value.HasValue) NextReviewDateUtc = value.Value; }
    }
    public int ReviewCount { get; set; } = 0;
    public int CorrectCount { get; set; } = 0;
    public int WrongCount { get; set; } = 0;
    public DateTime? LastReviewedAtUtc { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public FlashcardState State { get; set; } = FlashcardState.New;

    // Provenance
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourceDocumentChunkId { get; set; }
    public Guid? SourcePageId { get; set; }
    public Guid? SourceNoteId { get; set; }
    public Guid? AiGenerationId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User? User { get; set; }
    public StudyTopic? Topic { get; set; }
    public KnowledgeConcept? Concept { get; set; }

    public Flashcard() { }
    public Flashcard(Guid id) { Id = id; }
}

public class Quiz : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid? StudyTopicId { get; set; }
    public string Title { get; set; } = "Untitled Quiz";
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Medium"; // "Easy", "Medium", "Hard"

    // Provenance
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourcePageId { get; set; }
    public Guid? SourceNoteId { get; set; }
    public Guid? AiGenerationId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User? User { get; set; }
    public StudyTopic? Topic { get; set; }
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();

    public Quiz() { }
    public Quiz(Guid id) { Id = id; }
}

public class QuizQuestion : AuditableEntity
{
    public Guid QuizId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
    public string OptionsJson { get; set; } = "[]"; // JSON array of options for MCQ
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public int OrderIndex { get; set; } = 0;

    // Provenance
    public Guid? SourceDocumentId { get; set; }
    public Guid? SourcePageId { get; set; }
    public Guid? SourceNoteId { get; set; }

    public Quiz Quiz { get; set; } = null!;

    public QuizQuestion() { }
    public QuizQuestion(Guid id) { Id = id; }
}

public class QuizAttempt : AuditableEntity
{
    public Guid QuizId { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public double ScorePercentage { get; set; } = 0.0;
    public double Score { get; set; } = 0.0;
    public int TotalQuestions { get; set; } = 0;
    public int CorrectAnswers { get; set; } = 0;
    public bool IsCompleted { get; set; } = false;
    public string AnswersJson { get; set; } = "[]"; // User selected answers
    public string? AiFeedback { get; set; }

    public Quiz Quiz { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();

    public QuizAttempt() { }
    public QuizAttempt(Guid id) { Id = id; }
}

public class QuizAnswer : AuditableEntity
{
    public Guid QuizAttemptId { get; set; }
    public Guid QuizQuestionId { get; set; }
    public string SubmittedAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } = false;
    public string? Explanation { get; set; }

    public QuizAttempt QuizAttempt { get; set; } = null!;
    public QuizQuestion QuizQuestion { get; set; } = null!;

    public QuizAnswer() { }
    public QuizAnswer(Guid id) { Id = id; }
}
