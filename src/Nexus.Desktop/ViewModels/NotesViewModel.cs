using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private ObservableCollection<NoteSummaryDto> _notes = new();

    [ObservableProperty]
    private ObservableCollection<NoteSummaryDto> _filteredNotes = new();

    [ObservableProperty]
    private NoteSummaryDto? _selectedNoteSummary;

    [ObservableProperty]
    private NoteDto? _selectedNote;

    [ObservableProperty]
    private bool _hasSelectedNote;

    // Editor fields
    [ObservableProperty]
    private Guid? _currentNoteId;

    [ObservableProperty]
    private string _currentNoteTitle = string.Empty;

    [ObservableProperty]
    private string _currentNoteContent = string.Empty;

    [ObservableProperty]
    private string _currentNoteContentType = "markdown";

    [ObservableProperty]
    private bool _currentNoteIsPinned;

    [ObservableProperty]
    private Guid? _currentNotePageId;

    [ObservableProperty]
    private string _currentNoteTags = string.Empty;

    // Filters
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showOnlyPinned;

    [ObservableProperty]
    private Guid? _selectedPageFilterId;

    [ObservableProperty]
    private ObservableCollection<PageSummaryDto> _pageFilterOptions = new();

    // Async state
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isDeleting;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public NotesViewModel(IApiClient apiClient, UserSession userSession)
    {
        _apiClient = apiClient;
        _userSession = userSession;

        if (_userSession.SelectedWorkspace != null)
        {
            _ = InitializeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        await LoadPageFilterOptionsAsync();
        await LoadNotesAsync();
    }

    [RelayCommand]
    public async Task LoadPageFilterOptionsAsync()
    {
        if (_userSession.SelectedWorkspace == null) return;

        var treeResult = await _apiClient.GetPageTreeAsync(_userSession.SelectedWorkspace.Id);
        if (treeResult.IsSuccess)
        {
            PageFilterOptions.Clear();
            PageFilterOptions.Add(new PageSummaryDto(Guid.Empty, Guid.Empty, null, "All Pages", "📚", 0));

            void FlattenTree(IReadOnlyList<PageTreeNodeDto> nodes, string prefix = "")
            {
                foreach (var node in nodes)
                {
                    PageFilterOptions.Add(new PageSummaryDto(node.Id, node.WorkspaceId, node.ParentPageId, $"{prefix}{node.Icon} {node.Title}", node.Icon, node.OrderIndex));
                    if (node.Children.Count > 0)
                    {
                        FlattenTree(node.Children, prefix + "  └─ ");
                    }
                }
            }

            FlattenTree(treeResult.Value);
        }
    }

    [RelayCommand]
    public async Task LoadNotesAsync()
    {
        if (_userSession.SelectedWorkspace == null)
        {
            ErrorMessage = "No active workspace selected.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        Guid? pageIdParam = SelectedPageFilterId.HasValue && SelectedPageFilterId.Value != Guid.Empty
            ? SelectedPageFilterId.Value
            : null;

        bool? isPinnedParam = ShowOnlyPinned ? true : null;

        var result = await _apiClient.GetNotesAsync(_userSession.SelectedWorkspace.Id, pageIdParam, isPinnedParam);
        IsLoading = false;

        if (result.IsSuccess)
        {
            Notes.Clear();
            foreach (var note in result.Value)
            {
                Notes.Add(note);
            }
            ApplyLocalFilter();
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task SelectNoteAsync(NoteSummaryDto? summary)
    {
        if (summary == null || _userSession.SelectedWorkspace == null)
        {
            SelectedNoteSummary = null;
            SelectedNote = null;
            HasSelectedNote = false;
            return;
        }

        SelectedNoteSummary = summary;
        IsLoading = true;
        ErrorMessage = null;

        var result = await _apiClient.GetNoteByIdAsync(_userSession.SelectedWorkspace.Id, summary.Id);
        IsLoading = false;

        if (result.IsSuccess)
        {
            SelectedNote = result.Value;
            HasSelectedNote = true;

            CurrentNoteId = result.Value.Id;
            CurrentNoteTitle = result.Value.Title;
            CurrentNoteContent = result.Value.Content;
            CurrentNoteContentType = result.Value.ContentType;
            CurrentNoteIsPinned = result.Value.IsPinned;
            CurrentNotePageId = result.Value.PageId;
            CurrentNoteTags = string.Join(", ", result.Value.Tags);
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public void NewNote()
    {
        SelectedNoteSummary = null;
        SelectedNote = null;
        HasSelectedNote = true;

        CurrentNoteId = null;
        CurrentNoteTitle = "New Note";
        CurrentNoteContent = "# New Note\n\nWrite markdown notes here...";
        CurrentNoteContentType = "markdown";
        CurrentNoteIsPinned = false;
        CurrentNotePageId = SelectedPageFilterId.HasValue && SelectedPageFilterId.Value != Guid.Empty ? SelectedPageFilterId.Value : null;
        CurrentNoteTags = string.Empty;

        StatusMessage = "Drafting new note.";
        ErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveNoteAsync()
    {
        if (_userSession.SelectedWorkspace == null) return;

        if (string.IsNullOrWhiteSpace(CurrentNoteTitle))
        {
            ErrorMessage = "Note title cannot be empty.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = null;

        var tagsList = string.IsNullOrWhiteSpace(CurrentNoteTags)
            ? new List<string>()
            : CurrentNoteTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        Guid? pageIdParam = CurrentNotePageId.HasValue && CurrentNotePageId.Value != Guid.Empty ? CurrentNotePageId.Value : null;

        if (CurrentNoteId.HasValue)
        {
            // Update existing note
            var updateRequest = new UpdateNoteRequest(
                CurrentNoteTitle.Trim(),
                CurrentNoteContent ?? string.Empty,
                CurrentNoteContentType,
                CurrentNoteIsPinned,
                pageIdParam,
                tagsList);

            var result = await _apiClient.UpdateNoteAsync(_userSession.SelectedWorkspace.Id, CurrentNoteId.Value, updateRequest);
            IsSaving = false;

            if (result.IsSuccess)
            {
                SelectedNote = result.Value;
                await LoadNotesAsync();
                StatusMessage = "Note saved successfully.";
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        else
        {
            // Create new note
            var createRequest = new CreateNoteRequest(
                CurrentNoteTitle.Trim(),
                CurrentNoteContent ?? string.Empty,
                CurrentNoteContentType,
                CurrentNoteIsPinned,
                pageIdParam,
                tagsList);

            var result = await _apiClient.CreateNoteAsync(_userSession.SelectedWorkspace.Id, createRequest);
            IsSaving = false;

            if (result.IsSuccess)
            {
                CurrentNoteId = result.Value.Id;
                SelectedNote = result.Value;
                await LoadNotesAsync();
                await SelectNoteByIdAsync(result.Value.Id);
                StatusMessage = "Note created successfully.";
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
    }

    [RelayCommand]
    public async Task TogglePinAsync()
    {
        CurrentNoteIsPinned = !CurrentNoteIsPinned;
        if (CurrentNoteId.HasValue)
        {
            await SaveNoteAsync();
        }
    }

    [RelayCommand]
    public async Task DeleteNoteAsync()
    {
        if (_userSession.SelectedWorkspace == null || !CurrentNoteId.HasValue) return;

        IsDeleting = true;
        ErrorMessage = null;
        StatusMessage = null;

        var noteTitle = CurrentNoteTitle;
        var result = await _apiClient.DeleteNoteAsync(_userSession.SelectedWorkspace.Id, CurrentNoteId.Value);
        IsDeleting = false;

        if (result.IsSuccess)
        {
            CurrentNoteId = null;
            SelectedNote = null;
            SelectedNoteSummary = null;
            HasSelectedNote = false;
            await LoadNotesAsync();
            StatusMessage = $"Note '{noteTitle}' was deleted.";
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public void ApplyLocalFilter()
    {
        FilteredNotes.Clear();
        var query = Notes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLowerInvariant();
            query = query.Where(n =>
                n.Title.ToLowerInvariant().Contains(term) ||
                n.ContentSnippet.ToLowerInvariant().Contains(term) ||
                n.Tags.Any(t => t.ToLowerInvariant().Contains(term)));
        }

        foreach (var note in query)
        {
            FilteredNotes.Add(note);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyLocalFilter();
    }

    partial void OnShowOnlyPinnedChanged(bool value)
    {
        _ = LoadNotesAsync();
    }

    partial void OnSelectedPageFilterIdChanged(Guid? value)
    {
        _ = LoadNotesAsync();
    }

    private async Task SelectNoteByIdAsync(Guid noteId)
    {
        var summary = Notes.FirstOrDefault(n => n.Id == noteId);
        if (summary != null)
        {
            await SelectNoteAsync(summary);
        }
    }
}
