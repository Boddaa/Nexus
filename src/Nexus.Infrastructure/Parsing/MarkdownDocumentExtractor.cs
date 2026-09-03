using System.Text;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Common;

namespace Nexus.Infrastructure.Parsing;

public class MarkdownDocumentExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string extension, string contentType)
    {
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return Result.Success(text ?? string.Empty);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(new Error("Document.MarkdownExtractionFailed", $"Failed to extract markdown text: {ex.Message}"));
        }
    }
}
