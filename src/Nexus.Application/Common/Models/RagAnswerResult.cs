using Nexus.Application.DTOs.Conversations;

namespace Nexus.Application.Common.Models;

public record RagAnswerResult(
    string Answer,
    IReadOnlyList<ChatSourceDto> Sources,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null,
    IReadOnlyList<ChatSourceDto>? CitedSources = null);
