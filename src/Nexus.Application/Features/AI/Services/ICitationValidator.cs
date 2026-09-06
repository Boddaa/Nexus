using Nexus.Application.DTOs.Conversations;

namespace Nexus.Application.Features.AI.Services;

public record CitationValidationResult(
    string SanitizedText,
    IReadOnlyList<int> ValidSourceIndices,
    IReadOnlyList<int> InvalidSourceIndices,
    IReadOnlyList<ChatSourceDto> CitedSources
);

public interface ICitationValidator
{
    CitationValidationResult ValidateAndSanitize(string content, IReadOnlyList<ChatSourceDto> availableSources);
}
