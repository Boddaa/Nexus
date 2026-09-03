using System.IO.Compression;
using System.Xml.Linq;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Common;

namespace Nexus.Infrastructure.Parsing;

public class DocxDocumentExtractor : IDocumentTextExtractor
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public bool CanHandle(string extension, string contentType)
    {
        return string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);
    }

    public Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var documentEntry = archive.GetEntry("word/document.xml");

            if (documentEntry == null)
            {
                return Task.FromResult(Result.Failure<string>(new Error("Document.InvalidDocx", "The DOCX file is missing word/document.xml.")));
            }

            using var entryStream = documentEntry.Open();
            var xDoc = XDocument.Load(entryStream);

            var paragraphs = new List<string>();
            foreach (var p in xDoc.Descendants(W + "p"))
            {
                var text = string.Concat(p.Descendants(W + "t").Select(t => t.Value));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    paragraphs.Add(text.Trim());
                }
            }

            var fullText = string.Join("\n\n", paragraphs);
            return Task.FromResult(Result.Success(fullText));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<string>(new Error("Document.DocxExtractionFailed", $"Failed to extract DOCX text: {ex.Message}")));
        }
    }
}
