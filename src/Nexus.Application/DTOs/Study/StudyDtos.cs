using Nexus.Application.DTOs.Conversations;
using Nexus.Domain.Enums;

namespace Nexus.Application.DTOs.Study;

// ==================== Topic DTOs ====================

public record StudyTopicDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    string Title,
    string? Description,
    Guid? SourceDocumentId,
    Guid? SourcePageId,
    Guid? SourceNoteId,
    int FlashcardCount,
    int QuizCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CreateStudyTopicRequest(
    string Title,
    string? Description = null,
    Guid? SourceDocumentId = null,
    Guid? SourcePageId = null,
    Guid? SourceNoteId = null
);

public record UpdateStudyTopicRequest(
    string Title,
    string? Description = null
);

// ==================== Session DTOs ====================

public record StudySessionDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    Guid? StudyTopicId,
    string Title,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    DateTime? CompletedAtUtc,
    int DurationMinutes,
    StudySessionStatus Status,
    int ItemsAttempted,
    int ItemsCompleted,
    string? Notes
);

public record StartStudySessionRequest(
    string? Title = null,
    string? Notes = null
);

public record CompleteStudySessionRequest(
    int DurationMinutes = 0,
    int ItemsAttempted = 0,
    int ItemsCompleted = 0,
    string? Notes = null
);

// ==================== Flashcard DTOs ====================

public record FlashcardDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    Guid? StudyTopicId,
    Guid? ConceptId,
    string FrontText,
    string BackText,
    double EaseFactor,
    int Repetitions,
    int IntervalDays,
    DateTime NextReviewAtUtc,
    int ReviewCount,
    int CorrectCount,
    int WrongCount,
    DateTime? LastReviewedAtUtc,
    string Difficulty,
    FlashcardState State,
    Guid? SourceDocumentId,
    Guid? SourceDocumentChunkId,
    Guid? SourcePageId,
    Guid? SourceNoteId,
    Guid? AiGenerationId,
    DateTime CreatedAtUtc
);

public record CreateFlashcardRequest(
    string FrontText,
    string BackText,
    string Difficulty = "Medium",
    Guid? SourceDocumentId = null,
    Guid? SourcePageId = null,
    Guid? SourceNoteId = null
);

public record ReviewFlashcardRequest(
    ReviewRating Rating
);

public record GenerateFlashcardsRequest(
    string SourceType, // "Document", "Page", "Note", "Topic"
    Guid SourceId,
    int Count = 5,
    string Difficulty = "Medium",
    string? AdditionalInstructions = null
);

// ==================== Quiz DTOs ====================

public record QuizDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    Guid? StudyTopicId,
    string Title,
    string? Description,
    string DifficultyLevel,
    int QuestionCount,
    DateTime CreatedAtUtc
);

public record QuizQuestionDto(
    Guid Id,
    Guid QuizId,
    string QuestionText,
    string QuestionType,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? Explanation,
    string Difficulty,
    int OrderIndex,
    Guid? SourceDocumentId,
    Guid? SourcePageId,
    Guid? SourceNoteId
);

public record SafeQuizQuestionDto(
    Guid Id,
    Guid QuizId,
    string QuestionText,
    string QuestionType,
    IReadOnlyList<string> Options,
    string Difficulty,
    int OrderIndex
);

public record QuizDetailDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    Guid? StudyTopicId,
    string Title,
    string? Description,
    string DifficultyLevel,
    IReadOnlyList<QuizQuestionDto> Questions,
    DateTime CreatedAtUtc
);

public record SafeQuizDetailDto(
    Guid Id,
    Guid WorkspaceId,
    Guid UserId,
    Guid? StudyTopicId,
    string Title,
    string? Description,
    string DifficultyLevel,
    IReadOnlyList<SafeQuizQuestionDto> Questions,
    DateTime CreatedAtUtc
);

public record GenerateQuizRequest(
    string SourceType, // "Document", "Page", "Note", "Topic"
    Guid SourceId,
    int QuestionCount = 5,
    string DifficultyLevel = "Medium",
    string? AdditionalInstructions = null
);

public record CreateQuizAttemptRequest();

public record SubmitQuizAnswerDto(
    Guid QuestionId,
    string SubmittedAnswer
);

public record SubmitQuizAttemptRequest(
    IReadOnlyList<SubmitQuizAnswerDto> Answers
);

public record QuizAnswerResultDto(
    Guid QuestionId,
    string QuestionText,
    string SubmittedAnswer,
    string CorrectAnswer,
    bool IsCorrect,
    string? Explanation
);

public record QuizAttemptResultDto(
    Guid AttemptId,
    Guid QuizId,
    Guid UserId,
    Guid WorkspaceId,
    double ScorePercentage,
    double Score,
    int TotalQuestions,
    int CorrectAnswers,
    bool IsCompleted,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    IReadOnlyList<QuizAnswerResultDto> Answers,
    string? AiFeedback
);

// ==================== Assessment & Dashboard DTOs ====================

public record TopicPerformanceDto(
    Guid TopicId,
    string TopicTitle,
    int TotalFlashcards,
    int DueFlashcards,
    int ReviewedFlashcards,
    double FlashcardAccuracy,
    int TotalQuizzes,
    int CompletedAttempts,
    double AverageQuizScore,
    double MasteryPercentage,
    string MasteryLevel // "Strong", "Developing", "Weak"
);

public record KnowledgeAssessmentDto(
    Guid WorkspaceId,
    Guid UserId,
    Guid? TopicId,
    double OverallMasteryPercentage,
    double OverallAccuracy,
    int TotalFlashcards,
    int DueFlashcards,
    int TotalQuizzes,
    int TotalAttempts,
    IReadOnlyList<TopicPerformanceDto> StrongAreas,
    IReadOnlyList<TopicPerformanceDto> WeakAreas,
    IReadOnlyList<TopicPerformanceDto> DevelopingAreas,
    IReadOnlyList<string> RecommendedActions
);

public record StudyDashboardDto(
    int TotalTopics,
    int TotalSessions,
    int TotalStudyMinutes,
    int TotalFlashcards,
    int DueFlashcards,
    int TotalQuizzes,
    int CompletedAttempts,
    double AverageQuizScore,
    double OverallMasteryPercentage,
    IReadOnlyList<TopicPerformanceDto> RecentTopics,
    IReadOnlyList<FlashcardDto> DueFlashcardPreviews
);

// ==================== AI Tutor DTOs ====================

public record TutorChatRequest(
    string Message,
    Guid? StudyTopicId = null,
    Guid? ConversationId = null
);

public record TutorResponseDto(
    Guid ConversationId,
    string AssistantMessage,
    IReadOnlyList<ChatSourceDto> Sources,
    IReadOnlyList<string> KeyTakeaways,
    IReadOnlyList<string> FollowUpSuggestions
);

public record TutorHintRequest(
    Guid QuestionId,
    string? CurrentDraftAnswer = null
);

public record TutorHintDto(
    string Hint,
    int HintLevel
);

public record TutorExplainWrongAnswerRequest(
    Guid QuestionId,
    string SubmittedAnswer
);

public record TutorExplanationDto(
    string Explanation,
    IReadOnlyList<string> RemedialSteps,
    IReadOnlyList<ChatSourceDto> Sources
);

public record TutorMiniExerciseRequest(
    Guid? StudyTopicId = null,
    string? TargetConcept = null,
    string Difficulty = "Medium"
);

public record TutorMiniExerciseDto(
    string Question,
    string ExerciseType,
    IReadOnlyList<string>? Options,
    string Answer,
    string Explanation
);
