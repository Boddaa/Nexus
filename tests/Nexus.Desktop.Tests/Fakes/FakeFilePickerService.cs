using Nexus.Desktop.Services;

namespace Nexus.Desktop.Tests.Fakes;

public class FakeFilePickerService : IFilePickerService
{
    public PickedFileResult? NextPickedFile { get; set; }
    public string? NextSavePath { get; set; }
    public int PickForOpenCallCount { get; private set; }
    public int PickForSaveCallCount { get; private set; }

    public PickedFileResult? PickFileForOpen(string filter = "Supported Documents (*.pdf;*.docx;*.md;*.txt)|*.pdf;*.docx;*.md;*.txt|PDF Documents (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx|Markdown (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*")
    {
        PickForOpenCallCount++;
        return NextPickedFile;
    }

    public string? PickFileForSave(string defaultFileName, string filter = "All Files (*.*)|*.*")
    {
        PickForSaveCallCount++;
        return NextSavePath;
    }
}
