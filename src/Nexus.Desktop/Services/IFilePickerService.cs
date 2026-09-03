using System.IO;

namespace Nexus.Desktop.Services;

public record PickedFileResult(
    string FilePath,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Func<Stream> OpenRead);

public interface IFilePickerService
{
    PickedFileResult? PickFileForOpen(string filter = "Supported Documents (*.pdf;*.docx;*.md;*.txt)|*.pdf;*.docx;*.md;*.txt|PDF Documents (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx|Markdown (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*");
    string? PickFileForSave(string defaultFileName, string filter = "All Files (*.*)|*.*");
}
