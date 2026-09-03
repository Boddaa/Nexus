using System.Text;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Common;

namespace Nexus.Infrastructure.Parsing;

public class PlainTextDocumentExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string extension, string contentType)
    {
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Result<DocumentExtractionResult>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
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
            return Result.Success(new DocumentExtractionResult(text ?? string.Empty, null));
        }
        catch (Exception ex)
        {
            return Result.Failure<DocumentExtractionResult>(new Error("Document.TextExtractionFailed", $"Failed to extract plain text: {ex.Message}"));
        }
    }
}
