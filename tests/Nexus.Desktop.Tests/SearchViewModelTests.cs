using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Search;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Nexus.Domain.Common;
using Xunit;

namespace Nexus.Desktop.Tests;

public class SearchViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeNavigationService _fakeNavigationService;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public SearchViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeNavigationService = new FakeNavigationService();
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
    public void OpenResult_Page_Should_Navigate_To_PagesViewModel()
    {
        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
        var pageResult = new SearchResultDto(Guid.NewGuid(), "Page", _workspaceId, null, "Page Title", "Snippet", 100, DateTime.UtcNow, null);

        vm.OpenResult(pageResult);

        Assert.Equal(typeof(PagesViewModel), _fakeNavigationService.LastNavigatedType);
    }

    [Fact]
    public void OpenResult_Note_Should_Navigate_To_NotesViewModel()
    {
        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
        var noteResult = new SearchResultDto(Guid.NewGuid(), "Note", _workspaceId, null, "Note Title", "Snippet", 100, DateTime.UtcNow, null);

        vm.OpenResult(noteResult);

        Assert.Equal(typeof(NotesViewModel), _fakeNavigationService.LastNavigatedType);
    }

    [Fact]
    public void OpenResult_Document_Should_Navigate_To_DocumentsViewModel()
    {
        var vm = new SearchViewModel(_fakeApiClient, _fakeNavigationService, _userSession);
        var docResult = new SearchResultDto(Guid.NewGuid(), "Document", _workspaceId, null, "Doc Title", "Snippet", 100, DateTime.UtcNow, null);

        vm.OpenResult(docResult);

        Assert.Equal(typeof(DocumentsViewModel), _fakeNavigationService.LastNavigatedType);
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
}
