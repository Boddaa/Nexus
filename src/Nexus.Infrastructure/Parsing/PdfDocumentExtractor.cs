using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Common;
using UglyToad.PdfPig;

namespace Nexus.Infrastructure.Parsing;

public class PdfDocumentExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string extension, string contentType)
    {
        return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<Result<DocumentExtractionResult>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using var pdf = PdfDocument.Open(content);
            var pageCount = pdf.NumberOfPages;
            var pageTexts = new List<string>();

            foreach (var page in pdf.GetPages())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromResult(Result.Failure<DocumentExtractionResult>(new Error("Document.ExtractionCancelled", "PDF extraction was cancelled.")));
                }

                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    pageTexts.Add(text);
                }
            }

            var fullText = string.Join("\n\n", pageTexts);
            return Task.FromResult(Result.Success(new DocumentExtractionResult(fullText, pageCount)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<DocumentExtractionResult>(new Error("Document.PdfExtractionFailed", $"Failed to extract PDF text: {ex.Message}")));
        }
    }
}
