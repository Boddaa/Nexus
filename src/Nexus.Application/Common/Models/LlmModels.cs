namespace Nexus.Application.Common.Models;

public record LLMChatMessage(string Role, string Content);

public record LLMRequest(
    IReadOnlyList<LLMChatMessage> Messages,
    string? SystemPrompt = null,
    string? Context = null,
    double? Temperature = null,
    int? MaxTokens = null,
    string? Model = null);

public record LLMResponse(
    string Content,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null);
