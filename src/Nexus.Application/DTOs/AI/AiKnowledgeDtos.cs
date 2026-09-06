using Nexus.Application.DTOs.Conversations;

namespace Nexus.Application.DTOs.AI;

public record AiKnowledgeRequest(
    string SourceType, // "Document", "Page", "Note"
    Guid SourceId,
    string? AdditionalInstructions = null
);

public record GenerateQuestionsRequest(
    string SourceType, // "Document", "Page", "Note"
    Guid SourceId,
    int QuestionCount = 5,
    string Difficulty = "Intermediate", // "Beginner", "Intermediate", "Advanced"
    bool IncludeAnswers = true,
    string? AdditionalInstructions = null
);

public record GenerateStudyMaterialRequest(
    string SourceType, // "Document", "Page", "Note"
    Guid SourceId,
    string? AdditionalInstructions = null
);

public record SaveAiOutputAsNoteRequest(
    Guid AiGenerationId,
    Guid PageId,
    string Title
);

public record GeneratedQuestionDto(
    Guid Id,
    string Question,
    string Difficulty,
    string? Answer,
    IReadOnlyList<ChatSourceDto>? Sources = null
);

public record AiOperationResultDto(
    Guid Id,
    string Operation,
    string Content,
    IReadOnlyList<string>? KeyPoints,
    IReadOnlyList<GeneratedQuestionDto>? Questions,
    IReadOnlyList<ChatSourceDto> Sources,
    string Model,
    DateTime CreatedAtUtc
);

public record AiGenerationSummaryDto(
    Guid Id,
    string Operation,
    string ContentSnippet,
    string? SourceType,
    Guid? SourceId,
    string Model,
    DateTime CreatedAtUtc
);
