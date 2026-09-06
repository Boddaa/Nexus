using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Common.Models;

public record ChatMessage(AiRole Role, string Content);

public record ChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? SystemPrompt = null,
    double Temperature = 0.7,
    int MaxTokens = 2000);

public record ChatResponse(
    string Content,
    int PromptTokens,
    int CompletionTokens);

public record RagRequest(
    Guid WorkspaceId,
    string Query,
    ContextType ContextType = ContextType.Workspace,
    Guid? ContextEntityId = null,
    int TopK = 5);

public record RagResponse(
    string Answer,
    IReadOnlyList<SourceReferenceDto> Citations,
    int PromptTokens,
    int CompletionTokens);

public record SourceReferenceDto(
    Guid? DocumentId,
    Guid? DocumentChunkId,
    Guid? PageId,
    Guid? NoteId,
    string SourceTitle,
    string Snippet,
    double RelevanceScore,
    int PageNumber);

public record GeneratedMindMap(
    string Title,
    IReadOnlyList<MindMapNodeDto> Nodes,
    IReadOnlyList<MindMapEdgeDto> Edges);

public record MindMapNodeDto(
    string Id,
    string Title,
    string? Description,
    string? ParentId,
    string? LinkedEntityType,
    Guid? LinkedEntityId);

public record MindMapEdgeDto(
    string SourceId,
    string TargetId,
    string? Label,
    string? RelationType);

public record GeneratedQuiz(
    string Title,
    string? Description,
    IReadOnlyList<GeneratedQuizQuestionDto> Questions);

public record GeneratedQuizQuestionDto(
    string QuestionText,
    QuestionType QuestionType,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string? Explanation);

public record GeneratedFlashcard(
    string FrontText,
    string BackText,
    string? ConceptName);
