using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Search;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedTypeFilter = "All";

    [ObservableProperty]
    private ObservableCollection<SearchResultDto> _results = new();

    [ObservableProperty]
    private SearchResultDto? _selectedResult;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 0;

    [ObservableProperty]
    private int _totalCount = 0;

    [ObservableProperty]
    private bool _hasResults = false;

    [ObservableProperty]
    private bool _hasSearched = false;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<string> TypeFilterOptions { get; } = new()
    {
        "All",
        "Page",
        "Note",
        "Document"
    };

    public SearchViewModel(
        IApiClient apiClient,
        INavigationService navigationService,
        UserSession userSession)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;
        _userSession = userSession;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        await ExecuteSearchPageAsync(1);
    }

    [RelayCommand]
    public async Task ExecuteSearchPageAsync(int page)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            ErrorMessage = "Please enter a search query.";
            return;
        }

        if (_userSession.SelectedWorkspace == null)
        {
            ErrorMessage = "Please select a workspace before searching.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = null;

            var typeParam = SelectedTypeFilter == "All" ? null : SelectedTypeFilter;
            var request = new SearchRequest(SearchQuery.Trim(), page, 20, typeParam);

            var result = await _apiClient.SearchAsync(_userSession.SelectedWorkspace.Id, request);

            if (result.IsSuccess)
            {
                Results.Clear();
                foreach (var item in result.Value.Items)
                {
                    Results.Add(item);
                }

                CurrentPage = result.Value.Page;
                TotalCount = result.Value.TotalCount;
                TotalPages = result.Value.TotalPages;
                HasResults = Results.Count > 0;
                HasSearched = true;

                if (!HasResults)
                {
                    StatusMessage = $"No matches found for '{SearchQuery.Trim()}'.";
                }
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SetTypeFilterAsync(string type)
    {
        if (SelectedTypeFilter == type) return;
        SelectedTypeFilter = type;

        if (HasSearched && !string.IsNullOrWhiteSpace(SearchQuery))
        {
            await ExecuteSearchPageAsync(1);
        }
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages && !IsBusy)
        {
            await ExecuteSearchPageAsync(CurrentPage + 1);
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CurrentPage > 1 && !IsBusy)
        {
            await ExecuteSearchPageAsync(CurrentPage - 1);
        }
    }

    [RelayCommand]
    public async Task OpenResult(SearchResultDto? result)
    {
        var target = result ?? SelectedResult;
        if (target == null) return;

        if (target.Type.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            _navigationService.NavigateTo<PagesViewModel>();
            if (_navigationService.CurrentViewModel is PagesViewModel pagesVm)
            {
                await pagesVm.SelectPageByIdAsync(target.Id);
            }
        }
        else if (target.Type.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            _navigationService.NavigateTo<NotesViewModel>();
            if (_navigationService.CurrentViewModel is NotesViewModel notesVm)
            {
                await notesVm.SelectNoteByIdAsync(target.Id);
            }
        }
        else if (target.Type.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            _navigationService.NavigateTo<DocumentsViewModel>();
            if (_navigationService.CurrentViewModel is DocumentsViewModel docsVm)
            {
                await docsVm.SelectDocumentByIdAsync(target.Id);
            }
        }
    }
}
