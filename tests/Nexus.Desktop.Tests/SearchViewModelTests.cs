using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Search;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Nexus.Domain.Common;
using Nexus.Domain.Enums;
using Xunit;

namespace Nexus.Desktop.Tests;

public class SearchViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeNavigationService _fakeNavigationService;
    private readonly FakeDialogService _fakeDialogService;
    private readonly FakeFilePickerService _fakeFilePicker;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public SearchViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeNavigationService = new FakeNavigationService();
        _fakeDialogService = new FakeDialogService();
        _fakeFilePicker = new FakeFilePickerService();
        _userSession = new UserSession
        {
            CurrentUser = new AuthResponse(Guid.NewGuid(), "tester@nexus.ai", "Tester", "User", "fake-token", DateTime.UtcNow.AddDays(1)),
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "Test Workspace", "Desc", "📁", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };
    }

    [Fact]
    public async Task SearchAsync_Should_Populate_Results_And_Update_States()
    {
        // Arrange
        var item1 = new SearchResultDto(Guid.NewGuid(), "Page", _workspaceId, null, "Architecture", "System specs", 150, DateTime.UtcNow, null);
        var item2 = new SearchResultDto(Guid.NewGuid(), "Note", _workspaceId, null, "Database Note", "SQL queries", 100, DateTime.UtcNow, null);
        _fakeApiClient.SearchResults.AddRange(new[] { item1, item2 });

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession)
        {
            SearchQuery = "Architecture"
        };

        // Act
        await vm.SearchAsync();

        // Assert
        Assert.True(vm.HasSearched);
        Assert.True(vm.HasResults);
        Assert.Equal(2, vm.Results.Count);
        Assert.Equal(2, vm.TotalCount);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task SearchAsync_Empty_Query_Should_Set_ErrorMessage()
    {
        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession)
        {
            SearchQuery = "   "
        };

        await vm.SearchAsync();

        Assert.False(vm.HasSearched);
        Assert.False(vm.HasResults);
        Assert.Equal("Please enter a search query.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SearchAsync_No_Workspace_Should_Set_ErrorMessage()
    {
        var sessionWithoutWorkspace = new UserSession
        {
            CurrentUser = _userSession.CurrentUser,
            SelectedWorkspace = null
        };

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, sessionWithoutWorkspace)
        {
            SearchQuery = "test"
        };

        await vm.SearchAsync();

        Assert.False(vm.HasSearched);
        Assert.Equal("Please select a workspace before searching.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SetTypeFilterAsync_Should_Update_Filter_And_Requery()
    {
        var itemPage = new SearchResultDto(Guid.NewGuid(), "Page", _workspaceId, null, "Overview", "Snippet", 100, DateTime.UtcNow, null);
        var itemNote = new SearchResultDto(Guid.NewGuid(), "Note", _workspaceId, null, "Meeting", "Snippet", 100, DateTime.UtcNow, null);
        _fakeApiClient.SearchResults.AddRange(new[] { itemPage, itemNote });

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession)
        {
            SearchQuery = "Overview"
        };

        await vm.SearchAsync();
        Assert.Equal(2, vm.Results.Count);

        // Act - filter to Note only
        await vm.SetTypeFilterAsync("Note");

        // Assert
        Assert.Equal("Note", vm.SelectedTypeFilter);
        Assert.Single(vm.Results);
        Assert.Equal("Note", vm.Results[0].Type);
    }

    [Fact]
    public async Task OpenResult_Page_Should_Navigate_And_Select_Page()
    {
        // Arrange
        var targetPageId = Guid.NewGuid();
        var pageDto = new PageDto(targetPageId, _workspaceId, null, "Target Page", "📄", null, "{\"text\":\"Page Content\"}", 0, DateTime.UtcNow, null, 0, 0);
        _fakeApiClient.Pages.Add(pageDto);

        var pagesVm = new PagesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        _fakeNavigationService.ViewModelResolver = type => type == typeof(PagesViewModel) ? pagesVm : null;

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
        var pageResult = new SearchResultDto(targetPageId, "Page", _workspaceId, null, "Target Page", "Snippet", 100, DateTime.UtcNow, null);

        // Act
        await vm.OpenResult(pageResult);

        // Assert
        Assert.Equal(typeof(PagesViewModel), _fakeNavigationService.LastNavigatedType);
        Assert.NotNull(pagesVm.SelectedPage);
        Assert.Equal(targetPageId, pagesVm.SelectedPage.Id);
        Assert.Equal("Target Page", pagesVm.EditorTitle);
    }

    [Fact]
    public async Task OpenResult_Note_Should_Navigate_And_Select_Note()
    {
        // Arrange
        var targetNoteId = Guid.NewGuid();
        var noteDto = new NoteDto(targetNoteId, _workspaceId, null, null, "Target Note", "Note Content", "markdown", false, DateTime.UtcNow, null, new List<string> { "tag1" });
        _fakeApiClient.Notes.Add(noteDto);

        var notesVm = new NotesViewModel(_fakeApiClient, _fakeDialogService, _userSession);
        _fakeNavigationService.ViewModelResolver = type => type == typeof(NotesViewModel) ? notesVm : null;

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
        var noteResult = new SearchResultDto(targetNoteId, "Note", _workspaceId, null, "Target Note", "Snippet", 100, DateTime.UtcNow, null);

        // Act
        await vm.OpenResult(noteResult);

        // Assert
        Assert.Equal(typeof(NotesViewModel), _fakeNavigationService.LastNavigatedType);
        Assert.NotNull(notesVm.SelectedNote);
        Assert.Equal(targetNoteId, notesVm.SelectedNote.Id);
        Assert.Equal("Target Note", notesVm.CurrentNoteTitle);
    }

    [Fact]
    public async Task OpenResult_Document_Should_Navigate_And_Select_Document()
    {
        // Arrange
        var targetDocId = Guid.NewGuid();
        var docDetail = new DocumentDetailDto(targetDocId, _workspaceId, null, null, "Target Doc", "target.pdf", "application/pdf", ".pdf", 2048, DocumentStatus.Processed, null, "Sample extracted text", 1, 30, DateTime.UtcNow, null);
        _fakeApiClient.DocumentDetails.Add(docDetail);

        var docsVm = new DocumentsViewModel(_fakeApiClient, _fakeDialogService, _fakeFilePicker, _userSession);
        _fakeNavigationService.ViewModelResolver = type => type == typeof(DocumentsViewModel) ? docsVm : null;

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
        var docResult = new SearchResultDto(targetDocId, "Document", _workspaceId, null, "Target Doc", "Snippet", 100, DateTime.UtcNow, null);

        // Act
        await vm.OpenResult(docResult);

        // Assert
        Assert.Equal(typeof(DocumentsViewModel), _fakeNavigationService.LastNavigatedType);
        Assert.NotNull(docsVm.SelectedDocument);
        Assert.Equal(targetDocId, docsVm.SelectedDocument.Id);
        Assert.Equal("Target Doc", docsVm.SelectedDocument.Title);
    }

    [Fact]
    public async Task SearchAsync_Api_Failure_Should_Set_ErrorMessage_And_Reset_Busy()
    {
        _fakeApiClient.ShouldFail = true;
        _fakeApiClient.FailureError = new Error("Search.Failed", "Simulated search failure.");

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession)
        {
            SearchQuery = "Failing Query"
        };

        await vm.SearchAsync();

        Assert.False(vm.IsBusy);
        Assert.Equal("Simulated search failure.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SetSearchModeAsync_Should_Update_SelectedSearchMode_And_Requery()
    {
        var item1 = new SearchResultDto(Guid.NewGuid(), "Document", _workspaceId, null, "AI Arch", "Vector snippet", 88, DateTime.UtcNow, null, Guid.NewGuid(), "Semantic", 0.88);
        _fakeApiClient.SearchResults.Add(item1);

        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession)
        {
            SearchQuery = "AI"
        };

        await vm.SearchAsync();
        Assert.Equal("Keyword", vm.SelectedSearchMode);
        Assert.Single(vm.Results);

        // Switch to Semantic mode
        await vm.SetSearchModeAsync("Semantic");

        Assert.Equal("Semantic", vm.SelectedSearchMode);
        Assert.True(vm.HasSearched);
    }

    [Fact]
    public async Task SearchModeOptions_Should_Contain_Keyword_Semantic_And_Hybrid()
    {
        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);

        Assert.Contains("Keyword", vm.SearchModeOptions);
        Assert.Contains("Semantic", vm.SearchModeOptions);
        Assert.Contains("Hybrid", vm.SearchModeOptions);
    }
}
