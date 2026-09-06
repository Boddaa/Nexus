using System.Text;
using Nexus.Application.DTOs.Conversations;

namespace Nexus.Application.Features.AI.Services;

public class ContextBuilder : IContextBuilder
{
    public string BuildContext(IReadOnlyList<ChatSourceDto> sources, int maxCharacters = 6000)
    {
        if (sources == null || sources.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var blockBuilder = new StringBuilder();

            blockBuilder.AppendLine($"[SOURCE {i + 1}]");
            blockBuilder.AppendLine($"Title: {source.Title}");
            blockBuilder.AppendLine($"Type: {source.SourceType}");
            if (source.PageNumber.HasValue)
            {
                blockBuilder.AppendLine($"Page: {source.PageNumber.Value}");
            }
            blockBuilder.AppendLine("Content:");
            blockBuilder.AppendLine(source.Snippet?.Trim() ?? string.Empty);
            blockBuilder.AppendLine();

            var block = blockBuilder.ToString();

            if (sb.Length + block.Length > maxCharacters)
            {
                var remaining = maxCharacters - sb.Length;
                const string truncationNotice = "\n[... additional context truncated due to length limits ...]";
                if (remaining > truncationNotice.Length + 30)
                {
                    // Append partially accounting for the truncation notice
                    var takeLength = remaining - truncationNotice.Length;
                    sb.Append(block.AsSpan(0, takeLength));
                    sb.Append(truncationNotice);
                }
                else if (sb.Length == 0)
                {
                    // Even the first block exceeds maxCharacters
                    if (maxCharacters > truncationNotice.Length)
                    {
                        sb.Append(block.AsSpan(0, maxCharacters - truncationNotice.Length));
                        sb.Append(truncationNotice);
                    }
                    else
                    {
                        sb.Append(block.AsSpan(0, Math.Min(block.Length, maxCharacters)));
                    }
                }
                break;
            }

            sb.Append(block);
        }

        return sb.ToString().TrimEnd();
    }
}
