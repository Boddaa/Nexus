using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Nexus.Domain.Common;
using Xunit;

namespace Nexus.Desktop.Tests;

public class PagesAndNotesViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeDialogService _fakeDialogService;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public PagesAndNotesViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeDialogService = new FakeDialogService();
        _userSession = new UserSession
        {
            CurrentUser = new AuthResponse(Guid.NewGuid(), "test@nexus.ai", "Test User", "User", "fake.token", DateTime.UtcNow.AddDays(1)),
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "Main Workspace", "Description", "🚀", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };
    }

    #region PagesViewModel Tests

    [Fact]
    public async Task PagesViewModel_LoadPageTreeAsync_Should_Populate_PageTree()
    {
        // Arrange
        var rootPageId = Guid.NewGuid();
        var childPageId = Guid.NewGuid();
        _fakeApiClient.PageTrees.Add(new PageTreeNodeDto(
            rootPageId,
            _workspaceId,
            null,
            "Root Concept",
            "🧠",
            0,
            new List<PageTreeNodeDto>
            {
                new(childPageId, _workspaceId, rootPageId, "Sub Concept", "📄", 0, new List<PageTreeNodeDto>())
            }));

        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);

        // Act
        await vm.LoadPageTreeAsync();

        // Assert
        Assert.Single(vm.PageTree);
        Assert.Equal("Root Concept", vm.PageTree[0].Title);
        Assert.Single(vm.PageTree[0].Children);
        Assert.Equal("Sub Concept", vm.PageTree[0].Children[0].Title);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task PagesViewModel_SelectPageAsync_Should_Populate_Editor_Fields()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = new PageDto(
            pageId,
            _workspaceId,
            null,
            "Neural Networks",
            "⚡",
            "https://nexus.ai/cover.png",
            "{\"blocks\": []}",
            2,
            DateTime.UtcNow,
            null,
            1,
            5);
        _fakeApiClient.Pages.Add(page);

        var node = new PageTreeNodeDto(pageId, _workspaceId, null, "Neural Networks", "⚡", 2, new List<PageTreeNodeDto>());
        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);

        // Act
        await vm.SelectPageAsync(node);

        // Assert
        Assert.True(vm.HasSelectedPage);
        Assert.NotNull(vm.SelectedPage);
        Assert.Equal("Neural Networks", vm.EditorTitle);
        Assert.Equal("⚡", vm.EditorIcon);
        Assert.Equal("{\"blocks\": []}", vm.EditorContentJson);
        Assert.Equal(2, vm.EditorOrderIndex);
        Assert.Equal(1, vm.ChildPagesCount);
        Assert.Equal(5, vm.NotesCount);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task PagesViewModel_CreatePageAsync_Should_Add_Page_And_Select_It()
    {
        // Arrange
        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession)
        {
            NewPageTitle = "Deep Learning Fundamentals",
            NewPageIcon = "📘",
            NewPageOrderIndex = 1
        };

        // Act
        await vm.CreatePageAsync();

        // Assert
        Assert.False(vm.IsCreateDialogOpen);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("Deep Learning Fundamentals", vm.StatusMessage);
        Assert.Single(_fakeApiClient.Pages);
        Assert.Equal("Deep Learning Fundamentals", _fakeApiClient.Pages[0].Title);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task PagesViewModel_SavePageAsync_Should_Update_Page_And_Preserve_Selection()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = new PageDto(pageId, _workspaceId, null, "Initial Title", "📄", null, "{}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(page);

        var node = new PageTreeNodeDto(pageId, _workspaceId, null, "Initial Title", "📄", 0, new List<PageTreeNodeDto>());
        _fakeApiClient.PageTrees.Add(node);

        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectPageAsync(node);

        // Act - Edit and Save
        vm.EditorTitle = "Updated Title";
        vm.EditorIcon = "✨";
        await vm.SavePageAsync();

        // Assert
        Assert.Equal("Page saved successfully.", vm.StatusMessage);
        Assert.True(vm.HasSelectedPage);
        Assert.NotNull(vm.SelectedPage);
        Assert.Equal("Updated Title", vm.SelectedPage!.Title);
        Assert.Equal("✨", vm.SelectedPage.Icon);
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task PagesViewModel_MovePageAsync_Should_Exclude_Descendants_From_Targets()
    {
        // Arrange: A -> B -> C hierarchy
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var nodeC = new PageTreeNodeDto(idC, _workspaceId, idB, "Page C", "📄", 0, new List<PageTreeNodeDto>());
        var nodeB = new PageTreeNodeDto(idB, _workspaceId, idA, "Page B", "📄", 0, new List<PageTreeNodeDto> { nodeC });
        var nodeA = new PageTreeNodeDto(idA, _workspaceId, null, "Page A", "📄", 0, new List<PageTreeNodeDto> { nodeB });
        var nodeOther = new PageTreeNodeDto(otherId, _workspaceId, null, "Other Topic", "📁", 1, new List<PageTreeNodeDto>());

        _fakeApiClient.PageTrees.Add(nodeA);
        _fakeApiClient.PageTrees.Add(nodeOther);

        var pageA = new PageDto(idA, _workspaceId, null, "Page A", "📄", null, "{}", 0, DateTime.UtcNow, null, 1, 0);
        _fakeApiClient.Pages.Add(pageA);

        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.LoadPageTreeAsync();
        await vm.SelectPageAsync(nodeA);

        // Act: Open move dialog for Page A
        vm.OpenMoveDialog();

        // Assert: ParentPageOptions should ONLY contain Root and "Other Topic". A, B, C must be excluded.
        var availableIds = vm.ParentPageOptions.Select(p => p.Id).ToList();
        Assert.Contains(Guid.Empty, availableIds); // Root is available
        Assert.Contains(otherId, availableIds); // Other topic is available
        Assert.DoesNotContain(idA, availableIds); // Self excluded
        Assert.DoesNotContain(idB, availableIds); // Child excluded
        Assert.DoesNotContain(idC, availableIds); // Grandchild excluded
    }

    [Fact]
    public async Task PagesViewModel_DeletePageAsync_When_Cancelled_Should_Not_Call_Api()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = new PageDto(pageId, _workspaceId, null, "Keep Page", "📄", null, "{}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(page);
        var node = new PageTreeNodeDto(pageId, _workspaceId, null, "Keep Page", "📄", 0, new List<PageTreeNodeDto>());
        _fakeApiClient.PageTrees.Add(node);

        _fakeDialogService.ConfirmationResult = false; // User cancels dialog

        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectPageAsync(node);

        // Act
        await vm.DeletePageAsync();

        // Assert
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
        Assert.Single(_fakeApiClient.Pages); // Not deleted
        Assert.True(vm.HasSelectedPage);
        Assert.NotNull(vm.SelectedPage);
        Assert.False(vm.IsDeleting);
    }

    [Fact]
    public async Task PagesViewModel_DeletePageAsync_When_Confirmed_Should_Call_Api_And_Reset_Selection()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = new PageDto(pageId, _workspaceId, null, "Page to Delete", "📄", null, "{}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(page);
        var node = new PageTreeNodeDto(pageId, _workspaceId, null, "Page to Delete", "📄", 0, new List<PageTreeNodeDto>());
        _fakeApiClient.PageTrees.Add(node);

        _fakeDialogService.ConfirmationResult = true; // User confirms deletion

        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectPageAsync(node);

        // Act
        await vm.DeletePageAsync();

        // Assert
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
        Assert.Empty(_fakeApiClient.Pages);
        Assert.False(vm.HasSelectedPage);
        Assert.Null(vm.SelectedPage);
        Assert.Contains("Page to Delete", vm.StatusMessage!);
        Assert.False(vm.IsDeleting);
    }

    [Fact]
    public async Task PagesViewModel_Api_Failure_Resets_Loading_And_Sets_ErrorMessage()
    {
        // Arrange
        _fakeApiClient.ShouldFail = true;
        _fakeApiClient.FailureError = new Error("Network.Down", "Failed to connect to API.");

        var vm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);

        // Act
        await vm.LoadPageTreeAsync();

        // Assert
        Assert.False(vm.IsLoading);
        Assert.Equal("Failed to connect to API.", vm.ErrorMessage);
    }

    #endregion

    #region NotesViewModel Tests

    [Fact]
    public async Task NotesViewModel_LoadNotesAsync_Should_Populate_Notes_And_FilteredNotes()
    {
        // Arrange
        var noteId1 = Guid.NewGuid();
        var noteId2 = Guid.NewGuid();
        _fakeApiClient.NoteSummaries.Add(new NoteSummaryDto(noteId1, _workspaceId, null, null, "Backpropagation", "Derivation of gradient...", "markdown", true, DateTime.UtcNow, null, new List<string> { "Math", "ML" }));
        _fakeApiClient.NoteSummaries.Add(new NoteSummaryDto(noteId2, _workspaceId, null, null, "Attention Mechanism", "Transformers architecture...", "markdown", false, DateTime.UtcNow, null, new List<string> { "NLP" }));

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);

        // Act
        await vm.LoadNotesAsync();

        // Assert
        Assert.Equal(2, vm.Notes.Count);
        Assert.Equal(2, vm.FilteredNotes.Count);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task NotesViewModel_SearchText_Should_Filter_Notes_Locally_With_OrdinalIgnoreCase()
    {
        // Arrange
        var noteId1 = Guid.NewGuid();
        var noteId2 = Guid.NewGuid();
        _fakeApiClient.NoteSummaries.Add(new NoteSummaryDto(noteId1, _workspaceId, null, null, "Backpropagation", "Derivation of gradient...", "markdown", true, DateTime.UtcNow, null, new List<string> { "Math", "ML" }));
        _fakeApiClient.NoteSummaries.Add(new NoteSummaryDto(noteId2, _workspaceId, null, null, "Attention Mechanism", "Transformers architecture...", "markdown", false, DateTime.UtcNow, null, new List<string> { "NLP" }));

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.LoadNotesAsync();

        // Act
        vm.SearchText = "ATTENTION";

        // Assert
        Assert.Single(vm.FilteredNotes);
        Assert.Equal("Attention Mechanism", vm.FilteredNotes[0].Title);

        // Act - Search by tag
        vm.SearchText = "math";

        // Assert
        Assert.Single(vm.FilteredNotes);
        Assert.Equal("Backpropagation", vm.FilteredNotes[0].Title);
    }

    [Fact]
    public async Task NotesViewModel_SaveNoteAsync_Should_Update_And_Refresh_SelectedNoteSummary()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "Old Title", "Old Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);
        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "Old Title", "Old Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.NoteSummaries.Add(summary);

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.LoadNotesAsync();
        await vm.SelectNoteAsync(summary);

        // Act
        vm.CurrentNoteTitle = "Updated Note Title";
        vm.CurrentNoteContent = "Updated markdown content";
        await vm.SaveNoteAsync();

        // Assert
        Assert.Equal("Note saved successfully.", vm.StatusMessage);
        Assert.True(vm.HasSelectedNote);
        Assert.NotNull(vm.SelectedNoteSummary);
        Assert.Equal(noteId, vm.SelectedNoteSummary!.Id);
        Assert.Equal("Updated Note Title", vm.SelectedNoteSummary.Title);
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task NotesViewModel_TogglePinAsync_On_Success_Updates_State()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "Study Notes", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);
        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "Study Notes", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.NoteSummaries.Add(summary);

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectNoteAsync(summary);
        Assert.False(vm.CurrentNoteIsPinned);

        // Act
        await vm.TogglePinAsync();

        // Assert
        Assert.True(vm.CurrentNoteIsPinned);
        Assert.Equal("Note pinned.", vm.StatusMessage);
        Assert.True(_fakeApiClient.Notes.First(n => n.Id == noteId).IsPinned);
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task NotesViewModel_TogglePinAsync_On_Failure_Rolls_Back_State()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "Study Notes", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);
        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "Study Notes", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.NoteSummaries.Add(summary);

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectNoteAsync(summary);
        Assert.False(vm.CurrentNoteIsPinned);

        // Make API fail
        _fakeApiClient.ShouldFail = true;
        _fakeApiClient.FailureError = new Error("Notes.UpdateFailed", "Cannot update note.");

        // Act
        await vm.TogglePinAsync();

        // Assert - State rolled back to false
        Assert.False(vm.CurrentNoteIsPinned);
        Assert.Equal("Cannot update note.", vm.ErrorMessage);
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task NotesViewModel_DeleteNoteAsync_When_Cancelled_Should_Not_Call_Api()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "Keep Note", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);
        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "Keep Note", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.NoteSummaries.Add(summary);

        _fakeDialogService.ConfirmationResult = false; // User cancels

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectNoteAsync(summary);

        // Act
        await vm.DeleteNoteAsync();

        // Assert
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
        Assert.Single(_fakeApiClient.Notes);
        Assert.True(vm.HasSelectedNote);
        Assert.False(vm.IsDeleting);
    }

    [Fact]
    public async Task NotesViewModel_DeleteNoteAsync_When_Confirmed_Should_Call_Api_And_Reset_Selection()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "To Delete", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);
        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "To Delete", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.NoteSummaries.Add(summary);

        _fakeDialogService.ConfirmationResult = true; // User confirms

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        await vm.SelectNoteAsync(summary);

        // Act
        await vm.DeleteNoteAsync();

        // Assert
        Assert.Equal(1, _fakeDialogService.ConfirmCallCount);
        Assert.Empty(_fakeApiClient.Notes);
        Assert.False(vm.HasSelectedNote);
        Assert.Null(vm.CurrentNoteId);
        Assert.Contains("To Delete", vm.StatusMessage!);
        Assert.False(vm.IsDeleting);
    }

    [Fact]
    public async Task NotesViewModel_Api_Failure_Resets_Saving_And_Sets_ErrorMessage()
    {
        // Arrange
        _fakeApiClient.ShouldFail = true;
        _fakeApiClient.FailureError = new Error("Network.Unavailable", "Server timeout.");

        var vm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        vm.NewNote();
        vm.CurrentNoteTitle = "Test Note";

        // Act
        await vm.SaveNoteAsync();

        // Assert
        Assert.False(vm.IsSaving);
        Assert.Equal("Server timeout.", vm.ErrorMessage);
    }

    #endregion
}
