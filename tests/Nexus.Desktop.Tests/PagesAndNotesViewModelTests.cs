using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Xunit;

namespace Nexus.Desktop.Tests;

public class PagesAndNotesViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public PagesAndNotesViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
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

        var vm = new PagesViewModel(_fakeApiClient, _userSession);

        // Act
        await vm.LoadPageTreeAsync();

        // Assert
        Assert.Single(vm.PageTree);
        Assert.Equal("Root Concept", vm.PageTree[0].Title);
        Assert.Single(vm.PageTree[0].Children);
        Assert.Equal("Sub Concept", vm.PageTree[0].Children[0].Title);
        Assert.Null(vm.ErrorMessage);
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
        var vm = new PagesViewModel(_fakeApiClient, _userSession);

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
    }

    [Fact]
    public async Task PagesViewModel_CreatePageAsync_Should_Add_Page_And_Select_It()
    {
        // Arrange
        var vm = new PagesViewModel(_fakeApiClient, _userSession)
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
    }

    [Fact]
    public async Task PagesViewModel_SavePageAsync_Should_Update_Page()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = new PageDto(pageId, _workspaceId, null, "Initial Title", "📄", null, "{}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(page);

        var node = new PageTreeNodeDto(pageId, _workspaceId, null, "Initial Title", "📄", 0, new List<PageTreeNodeDto>());
        var vm = new PagesViewModel(_fakeApiClient, _userSession);
        await vm.SelectPageAsync(node);

        // Act - Edit and Save
        vm.EditorTitle = "Updated Title";
        vm.EditorIcon = "✨";
        await vm.SavePageAsync();

        // Assert
        Assert.Equal("Page saved successfully.", vm.StatusMessage);
        Assert.Equal("Updated Title", vm.SelectedPage!.Title);
        Assert.Equal("✨", vm.SelectedPage.Icon);
    }

    [Fact]
    public async Task PagesViewModel_MovePageAsync_Should_Call_Api_And_Refresh()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var page = new PageDto(childId, _workspaceId, null, "To Move", "📄", null, "{}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(page);

        var node = new PageTreeNodeDto(childId, _workspaceId, null, "To Move", "📄", 0, new List<PageTreeNodeDto>());
        var vm = new PagesViewModel(_fakeApiClient, _userSession);
        await vm.SelectPageAsync(node);

        // Act
        vm.OpenMoveDialog();
        vm.MoveTargetParentId = rootId;
        vm.MoveNewOrderIndex = 3;
        await vm.MovePageAsync();

        // Assert
        Assert.False(vm.IsMoveDialogOpen);
        Assert.Equal("Page moved successfully.", vm.StatusMessage);
        Assert.Equal(rootId, _fakeApiClient.Pages.First(p => p.Id == childId).ParentPageId);
        Assert.Equal(3, _fakeApiClient.Pages.First(p => p.Id == childId).OrderIndex);
    }

    [Fact]
    public async Task PagesViewModel_DeletePageAsync_Should_Remove_Page_And_Clear_Selection()
    {
        // Arrange
        var pageId = Guid.NewGuid();
        var page = new PageDto(pageId, _workspaceId, null, "Page to Delete", "📄", null, "{}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(page);

        var node = new PageTreeNodeDto(pageId, _workspaceId, null, "Page to Delete", "📄", 0, new List<PageTreeNodeDto>());
        var vm = new PagesViewModel(_fakeApiClient, _userSession);
        await vm.SelectPageAsync(node);

        // Act
        await vm.DeletePageAsync();

        // Assert
        Assert.False(vm.HasSelectedPage);
        Assert.Null(vm.SelectedPage);
        Assert.Empty(_fakeApiClient.Pages);
        Assert.Contains("Page to Delete", vm.StatusMessage!);
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

        var vm = new NotesViewModel(_fakeApiClient, _userSession);

        // Act
        await vm.LoadNotesAsync();

        // Assert
        Assert.Equal(2, vm.Notes.Count);
        Assert.Equal(2, vm.FilteredNotes.Count);
    }

    [Fact]
    public async Task NotesViewModel_SearchText_Should_Filter_Notes_Locally()
    {
        // Arrange
        var noteId1 = Guid.NewGuid();
        var noteId2 = Guid.NewGuid();
        _fakeApiClient.NoteSummaries.Add(new NoteSummaryDto(noteId1, _workspaceId, null, null, "Backpropagation", "Derivation of gradient...", "markdown", true, DateTime.UtcNow, null, new List<string> { "Math", "ML" }));
        _fakeApiClient.NoteSummaries.Add(new NoteSummaryDto(noteId2, _workspaceId, null, null, "Attention Mechanism", "Transformers architecture...", "markdown", false, DateTime.UtcNow, null, new List<string> { "NLP" }));

        var vm = new NotesViewModel(_fakeApiClient, _userSession);
        await vm.LoadNotesAsync();

        // Act
        vm.SearchText = "attention";

        // Assert
        Assert.Single(vm.FilteredNotes);
        Assert.Equal("Attention Mechanism", vm.FilteredNotes[0].Title);

        // Act - Search by tag
        vm.SearchText = "Math";

        // Assert
        Assert.Single(vm.FilteredNotes);
        Assert.Equal("Backpropagation", vm.FilteredNotes[0].Title);
    }

    [Fact]
    public async Task NotesViewModel_SelectNoteAsync_Should_Load_Details_Into_Editor()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(
            noteId,
            _workspaceId,
            null,
            null,
            "Transformer Models",
            "# Transformer Models\n\nSelf-attention is key.",
            "markdown",
            true,
            DateTime.UtcNow,
            null,
            new List<string> { "AI", "NLP" });
        _fakeApiClient.Notes.Add(note);

        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "Transformer Models", "Self-attention is key.", "markdown", true, DateTime.UtcNow, null, new List<string> { "AI", "NLP" });
        var vm = new NotesViewModel(_fakeApiClient, _userSession);

        // Act
        await vm.SelectNoteAsync(summary);

        // Assert
        Assert.True(vm.HasSelectedNote);
        Assert.Equal(noteId, vm.CurrentNoteId);
        Assert.Equal("Transformer Models", vm.CurrentNoteTitle);
        Assert.Equal("# Transformer Models\n\nSelf-attention is key.", vm.CurrentNoteContent);
        Assert.True(vm.CurrentNoteIsPinned);
        Assert.Equal("AI, NLP", vm.CurrentNoteTags);
    }

    [Fact]
    public async Task NotesViewModel_SaveNoteAsync_Should_Create_New_Note_When_CurrentNoteId_Is_Null()
    {
        // Arrange
        var vm = new NotesViewModel(_fakeApiClient, _userSession);
        vm.NewNote();
        vm.CurrentNoteTitle = "New Architecture Insight";
        vm.CurrentNoteContent = "Content of insight";
        vm.CurrentNoteTags = "Architecture, Design";
        vm.CurrentNoteIsPinned = true;

        // Act
        await vm.SaveNoteAsync();

        // Assert
        Assert.Equal("Note created successfully.", vm.StatusMessage);
        Assert.Single(_fakeApiClient.Notes);
        Assert.Equal("New Architecture Insight", _fakeApiClient.Notes[0].Title);
        Assert.True(_fakeApiClient.Notes[0].IsPinned);
        Assert.Equal(2, _fakeApiClient.Notes[0].Tags.Count);
    }

    [Fact]
    public async Task NotesViewModel_SaveNoteAsync_Should_Update_Existing_Note_When_CurrentNoteId_Is_Set()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "Old Title", "Old Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);

        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "Old Title", "Old Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        var vm = new NotesViewModel(_fakeApiClient, _userSession);
        await vm.SelectNoteAsync(summary);

        // Act
        vm.CurrentNoteTitle = "Updated Note Title";
        vm.CurrentNoteContent = "Updated markdown content";
        vm.CurrentNoteTags = "Updated";
        await vm.SaveNoteAsync();

        // Assert
        Assert.Equal("Note saved successfully.", vm.StatusMessage);
        Assert.Equal("Updated Note Title", _fakeApiClient.Notes.First(n => n.Id == noteId).Title);
        Assert.Equal("Updated markdown content", _fakeApiClient.Notes.First(n => n.Id == noteId).Content);
    }

    [Fact]
    public async Task NotesViewModel_DeleteNoteAsync_Should_Remove_Note_And_Reset_Selection()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        var note = new NoteDto(noteId, _workspaceId, null, null, "To Delete", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        _fakeApiClient.Notes.Add(note);

        var summary = new NoteSummaryDto(noteId, _workspaceId, null, null, "To Delete", "Content", "markdown", false, DateTime.UtcNow, null, new List<string>());
        var vm = new NotesViewModel(_fakeApiClient, _userSession);
        await vm.SelectNoteAsync(summary);

        // Act
        await vm.DeleteNoteAsync();

        // Assert
        Assert.False(vm.HasSelectedNote);
        Assert.Null(vm.CurrentNoteId);
        Assert.Empty(_fakeApiClient.Notes);
        Assert.Contains("To Delete", vm.StatusMessage!);
    }

    #endregion
}
