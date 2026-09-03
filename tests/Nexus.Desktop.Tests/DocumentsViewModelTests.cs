using System.IO;
using System.Text;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Nexus.Domain.Common;
using Nexus.Domain.Enums;
using Xunit;

namespace Nexus.Desktop.Tests;

public class DocumentsViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeDialogService _fakeDialogService;
    private readonly FakeFilePickerService _fakeFilePicker;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public DocumentsViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeDialogService = new FakeDialogService();
        _fakeFilePicker = new FakeFilePickerService();
        _userSession = new UserSession
        {
            CurrentUser = new AuthResponse(Guid.NewGuid(), "test@nexus.ai", "Test User", "User", "fake.token", DateTime.UtcNow.AddDays(1)),
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "Main Workspace", "Description", "🚀", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };
    }

    [Fact]
    public async Task DocumentsViewModel_LoadDocumentsAsync_Should_Populate_Collections()
    {
        // Arrange
        var doc1 = new DocumentSummaryDto(Guid.NewGuid(), _workspaceId, null, null, "Doc 1", "doc1.pdf", "application/pdf", ".pdf", 1024, DocumentStatus.Processed, DateTime.UtcNow);
        var doc2 = new DocumentSummaryDto(Guid.NewGuid(), _workspaceId, null, null, "Doc 2", "doc2.docx", "application/docx", ".docx", 2048, DocumentStatus.Processed, DateTime.UtcNow);
        _fakeApiClient.DocumentSummaries.AddRange(new[] { doc1, doc2 });

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);

        // Act
        await vm.LoadDocumentsAsync();

        // Assert
        Assert.Equal(2, vm.Documents.Count);
        Assert.Equal(2, vm.FilteredDocuments.Count);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task DocumentsViewModel_SelectDocumentAsync_Should_Populate_Details_And_ExtractedText()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var summary = new DocumentSummaryDto(docId, _workspaceId, null, null, "AI Notes", "notes.md", "text/markdown", ".md", 500, DocumentStatus.Processed, DateTime.UtcNow);
        var detail = new DocumentDetailDto(docId, _workspaceId, null, null, "AI Notes", "notes.md", "text/markdown", ".md", 500, DocumentStatus.Processed, null, "Extracted markdown text", 0, 23, DateTime.UtcNow, null);

        _fakeApiClient.DocumentSummaries.Add(summary);
        _fakeApiClient.DocumentDetails.Add(detail);

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);

        // Act
        await vm.SelectDocumentAsync(summary);

        // Assert
        Assert.True(vm.HasSelectedDocument);
        Assert.NotNull(vm.SelectedDocument);
        Assert.Equal("AI Notes", vm.SelectedDocument!.Title);
        Assert.Equal("Extracted markdown text", vm.SelectedDocument.ExtractedText);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task DocumentsViewModel_UploadDocumentAsync_When_File_Picked_Should_Upload_And_Select()
    {
        // Arrange
        var fileContent = "Uploaded test document";
        _fakeFilePicker.NextPickedFile = new PickedFileResult(
            "C:\\test\\notes.txt",
            "notes.txt",
            "text/plain",
            fileContent.Length,
            () => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);

        // Act
        await vm.UploadDocumentAsync();

        // Assert
        Assert.Equal(1, _fakeFilePicker.PickForOpenCallCount);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("notes.txt", vm.StatusMessage);
        Assert.Single(vm.Documents);
        Assert.True(vm.HasSelectedDocument);
        Assert.False(vm.IsUploading);
    }

    [Fact]
    public async Task DocumentsViewModel_DeleteDocumentAsync_When_Cancelled_Should_Not_Call_Api()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var summary = new DocumentSummaryDto(docId, _workspaceId, null, null, "Keep Doc", "keep.txt", "text/plain", ".txt", 100, DocumentStatus.Processed, DateTime.UtcNow);
        var detail = new DocumentDetailDto(docId, _workspaceId, null, null, "Keep Doc", "keep.txt", "text/plain", ".txt", 100, DocumentStatus.Processed, null, "Text", 0, 4, DateTime.UtcNow, null);

        _fakeApiClient.DocumentSummaries.Add(summary);
        _fakeApiClient.DocumentDetails.Add(detail);

        _fakeDialogService.ConfirmationResult = false; // User cancels

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);
        await vm.SelectDocumentAsync(summary);

        // Act
        await vm.DeleteDocumentAsync();

        // Assert
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
        Assert.Single(_fakeApiClient.DocumentSummaries); // Not deleted
        Assert.True(vm.HasSelectedDocument);
        Assert.False(vm.IsDeleting);
    }

    [Fact]
    public async Task DocumentsViewModel_DeleteDocumentAsync_When_Confirmed_Should_Call_Api_And_Reset_Selection()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var summary = new DocumentSummaryDto(docId, _workspaceId, null, null, "To Delete", "delete.txt", "text/plain", ".txt", 100, DocumentStatus.Processed, DateTime.UtcNow);
        var detail = new DocumentDetailDto(docId, _workspaceId, null, null, "To Delete", "delete.txt", "text/plain", ".txt", 100, DocumentStatus.Processed, null, "Text", 0, 4, DateTime.UtcNow, null);

        _fakeApiClient.DocumentSummaries.Add(summary);
        _fakeApiClient.DocumentDetails.Add(detail);

        _fakeDialogService.ConfirmationResult = true; // User confirms

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);
        await vm.SelectDocumentAsync(summary);

        // Act
        await vm.DeleteDocumentAsync();

        // Assert
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
        Assert.Empty(_fakeApiClient.DocumentSummaries);
        Assert.False(vm.HasSelectedDocument);
        Assert.Null(vm.SelectedDocument);
        Assert.Contains("delete.txt", vm.StatusMessage!);
        Assert.False(vm.IsDeleting);
    }

    [Fact]
    public async Task DocumentsViewModel_SearchText_Should_Filter_Documents_Locally()
    {
        // Arrange
        var doc1 = new DocumentSummaryDto(Guid.NewGuid(), _workspaceId, null, null, "Neural Networks", "nn.pdf", "application/pdf", ".pdf", 100, DocumentStatus.Processed, DateTime.UtcNow);
        var doc2 = new DocumentSummaryDto(Guid.NewGuid(), _workspaceId, null, null, "Database Schema", "db.docx", "application/docx", ".docx", 100, DocumentStatus.Processed, DateTime.UtcNow);
        _fakeApiClient.DocumentSummaries.AddRange(new[] { doc1, doc2 });

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);
        await vm.LoadDocumentsAsync();

        // Act
        vm.SearchText = "NEURAL";

        // Assert
        Assert.Single(vm.FilteredDocuments);
        Assert.Equal("nn.pdf", vm.FilteredDocuments[0].FileName);
    }

    [Fact]
    public async Task DocumentsViewModel_DownloadDocumentAsync_Should_Write_Bytes_To_Target()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var tempSavePath = Path.Combine(Path.GetTempPath(), "downloaded_" + Guid.NewGuid().ToString("N") + ".pdf");
        _fakeFilePicker.NextSavePath = tempSavePath;

        var summary = new DocumentSummaryDto(docId, _workspaceId, null, null, "Downloadable", "file.pdf", "application/pdf", ".pdf", 4, DocumentStatus.Processed, DateTime.UtcNow);
        var detail = new DocumentDetailDto(docId, _workspaceId, null, null, "Downloadable", "file.pdf", "application/pdf", ".pdf", 4, DocumentStatus.Processed, null, "text", 1, 4, DateTime.UtcNow, null);
        _fakeApiClient.DocumentSummaries.Add(summary);
        _fakeApiClient.DocumentDetails.Add(detail);

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);
        await vm.SelectDocumentAsync(summary);

        try
        {
            // Act
            await vm.DownloadDocumentAsync();

            // Assert
            Assert.Equal(1, _fakeFilePicker.PickForSaveCallCount);
            Assert.True(File.Exists(tempSavePath));
            var bytes = await File.ReadAllBytesAsync(tempSavePath);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
            Assert.Contains("downloaded successfully", vm.StatusMessage!);
        }
        finally
        {
            if (File.Exists(tempSavePath))
            {
                File.Delete(tempSavePath);
            }
        }
    }

    [Fact]
    public async Task DocumentsViewModel_Api_Failure_Resets_Busy_And_Sets_ErrorMessage()
    {
        // Arrange
        _fakeApiClient.ShouldFail = true;
        _fakeApiClient.FailureError = new Error("Api.Timeout", "Service is unavailable.");

        var vm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);

        // Act
        await vm.LoadDocumentsAsync();

        // Assert
        Assert.False(vm.IsLoading);
        Assert.False(vm.IsBusy);
        Assert.Equal("Service is unavailable.", vm.ErrorMessage);
    }
}
