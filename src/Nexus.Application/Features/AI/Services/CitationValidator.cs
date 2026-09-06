using System.Text.RegularExpressions;
using Nexus.Application.DTOs.Conversations;

namespace Nexus.Application.Features.AI.Services;

public class CitationValidator : ICitationValidator
{
    private static readonly Regex SourceTagRegex = new(@"\[SOURCE\s*(\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CitationValidationResult ValidateAndSanitize(string content, IReadOnlyList<ChatSourceDto> availableSources)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new CitationValidationResult(
                SanitizedText: content ?? string.Empty,
                ValidSourceIndices: Array.Empty<int>(),
                InvalidSourceIndices: Array.Empty<int>(),
                CitedSources: Array.Empty<ChatSourceDto>());
        }

        var sourcesCount = availableSources?.Count ?? 0;
        var validIndices = new HashSet<int>();
        var invalidIndices = new HashSet<int>();

        var sanitized = SourceTagRegex.Replace(content, match =>
        {
            if (int.TryParse(match.Groups[1].Value, out int sourceNum))
            {
                if (sourceNum >= 1 && sourceNum <= sourcesCount)
                {
                    validIndices.Add(sourceNum);
                    return $"[SOURCE {sourceNum}]";
                }
                else
                {
                    invalidIndices.Add(sourceNum);
                    return string.Empty;
                }
            }

            return string.Empty;
        });

        // Clean up redundant double spaces left by removing invalid tags
        sanitized = Regex.Replace(sanitized, @"[ ]{2,}", " ").Trim();

        var citedSources = validIndices
            .OrderBy(i => i)
            .Select(i => availableSources![i - 1])
            .ToList();

        return new CitationValidationResult(
            SanitizedText: sanitized,
            ValidSourceIndices: validIndices.OrderBy(i => i).ToList(),
            InvalidSourceIndices: invalidIndices.OrderBy(i => i).ToList(),
            CitedSources: citedSources);
    }
}
