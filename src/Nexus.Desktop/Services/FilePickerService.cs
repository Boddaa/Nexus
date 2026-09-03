using System.IO;
using Microsoft.Win32;

namespace Nexus.Desktop.Services;

public class FilePickerService : IFilePickerService
{
    public PickedFileResult? PickFileForOpen(string filter = "Supported Documents (*.pdf;*.docx;*.md;*.txt)|*.pdf;*.docx;*.md;*.txt|PDF Documents (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx|Markdown (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            var fileInfo = new FileInfo(dialog.FileName);
            var extension = fileInfo.Extension.ToLowerInvariant();
            var contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".md" => "text/markdown",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };

            return new PickedFileResult(
                dialog.FileName,
                fileInfo.Name,
                contentType,
                fileInfo.Length,
                () => File.OpenRead(dialog.FileName));
        }

        return null;
    }

    public string? PickFileForSave(string defaultFileName, string filter = "All Files (*.*)|*.*")
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
