using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Pages;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class PagesViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private ObservableCollection<PageTreeNodeDto> _pageTree = new();

    [ObservableProperty]
    private PageTreeNodeDto? _selectedTreeNode;

    [ObservableProperty]
    private PageDto? _selectedPage;

    [ObservableProperty]
    private bool _hasSelectedPage;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorIcon = "📄";

    [ObservableProperty]
    private string _editorContentJson = "{}";

    [ObservableProperty]
    private int _editorOrderIndex;

    [ObservableProperty]
    private string? _editorCoverImageUrl;

    [ObservableProperty]
    private int _childPagesCount;

    [ObservableProperty]
    private int _notesCount;

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

    // Create Modal State
    [ObservableProperty]
    private bool _isCreateDialogOpen;

    [ObservableProperty]
    private string _newPageTitle = "Untitled Page";

    [ObservableProperty]
    private string _newPageIcon = "📄";

    [ObservableProperty]
    private Guid? _newPageParentId;

    [ObservableProperty]
    private int _newPageOrderIndex;

    [ObservableProperty]
    private ObservableCollection<PageSummaryDto> _parentPageOptions = new();

    // Move Modal State
    [ObservableProperty]
    private bool _isMoveDialogOpen;

    [ObservableProperty]
    private Guid? _moveTargetParentId;

    [ObservableProperty]
    private int _moveNewOrderIndex;

    public PagesViewModel(IApiClient apiClient, UserSession userSession)
    {
        _apiClient = apiClient;
        _userSession = userSession;

        if (_userSession.SelectedWorkspace != null)
        {
            _ = LoadPageTreeAsync();
        }
    }

    [RelayCommand]
    public async Task LoadPageTreeAsync()
    {
        if (_userSession.SelectedWorkspace == null)
        {
            ErrorMessage = "No active workspace selected.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        var result = await _apiClient.GetPageTreeAsync(_userSession.SelectedWorkspace.Id);
        IsLoading = false;

        if (result.IsSuccess)
        {
            PageTree.Clear();
            foreach (var node in result.Value)
            {
                PageTree.Add(node);
            }
            PopulateParentOptions();
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task SelectPageAsync(PageTreeNodeDto? node)
    {
        if (node == null || _userSession.SelectedWorkspace == null)
        {
            SelectedTreeNode = null;
            SelectedPage = null;
            HasSelectedPage = false;
            return;
        }

        SelectedTreeNode = node;
        IsLoading = true;
        ErrorMessage = null;

        var result = await _apiClient.GetPageByIdAsync(_userSession.SelectedWorkspace.Id, node.Id);
        IsLoading = false;

        if (result.IsSuccess)
        {
            SelectedPage = result.Value;
            HasSelectedPage = true;

            EditorTitle = result.Value.Title;
            EditorIcon = result.Value.Icon;
            EditorContentJson = result.Value.ContentJson;
            EditorOrderIndex = result.Value.OrderIndex;
            EditorCoverImageUrl = result.Value.CoverImageUrl;
            ChildPagesCount = result.Value.ChildPagesCount;
            NotesCount = result.Value.NotesCount;
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public void OpenCreateDialog(string? asChildParam)
    {
        bool asChild = asChildParam == "child";
        NewPageTitle = "Untitled Page";
        NewPageIcon = "📄";
        NewPageOrderIndex = 0;
        NewPageParentId = asChild && SelectedPage != null ? SelectedPage.Id : null;

        PopulateParentOptions();
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    public void CloseCreateDialog()
    {
        IsCreateDialogOpen = false;
    }

    [RelayCommand]
    public async Task CreatePageAsync()
    {
        if (_userSession.SelectedWorkspace == null) return;

        if (string.IsNullOrWhiteSpace(NewPageTitle))
        {
            ErrorMessage = "Page title cannot be empty.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        var request = new CreatePageRequest(
            NewPageTitle.Trim(),
            string.IsNullOrWhiteSpace(NewPageIcon) ? "📄" : NewPageIcon.Trim(),
            null,
            "{}",
            NewPageParentId,
            NewPageOrderIndex);

        var result = await _apiClient.CreatePageAsync(_userSession.SelectedWorkspace.Id, request);
        IsLoading = false;

        if (result.IsSuccess)
        {
            IsCreateDialogOpen = false;
            await LoadPageTreeAsync();
            await SelectPageByIdAsync(result.Value.Id);
            StatusMessage = $"Page '{result.Value.Title}' created successfully.";
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task SavePageAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedPage == null) return;

        if (string.IsNullOrWhiteSpace(EditorTitle))
        {
            ErrorMessage = "Page title cannot be empty.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = null;

        var request = new UpdatePageRequest(
            EditorTitle.Trim(),
            string.IsNullOrWhiteSpace(EditorIcon) ? "📄" : EditorIcon.Trim(),
            EditorCoverImageUrl?.Trim(),
            EditorContentJson ?? "{}",
            EditorOrderIndex);

        var result = await _apiClient.UpdatePageAsync(_userSession.SelectedWorkspace.Id, SelectedPage.Id, request);
        IsSaving = false;

        if (result.IsSuccess)
        {
            SelectedPage = result.Value;
            await LoadPageTreeAsync();
            StatusMessage = "Page saved successfully.";
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public void OpenMoveDialog()
    {
        if (SelectedPage == null) return;

        MoveTargetParentId = SelectedPage.ParentPageId;
        MoveNewOrderIndex = SelectedPage.OrderIndex;
        PopulateParentOptions();
        IsMoveDialogOpen = true;
    }

    [RelayCommand]
    public void CloseMoveDialog()
    {
        IsMoveDialogOpen = false;
    }

    [RelayCommand]
    public async Task MovePageAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedPage == null) return;

        IsLoading = true;
        ErrorMessage = null;

        var request = new MovePageRequest(MoveTargetParentId, MoveNewOrderIndex);
        var result = await _apiClient.MovePageAsync(_userSession.SelectedWorkspace.Id, SelectedPage.Id, request);
        IsLoading = false;

        if (result.IsSuccess)
        {
            IsMoveDialogOpen = false;
            await LoadPageTreeAsync();
            await SelectPageByIdAsync(SelectedPage.Id);
            StatusMessage = "Page moved successfully.";
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task DeletePageAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedPage == null) return;

        IsDeleting = true;
        ErrorMessage = null;
        StatusMessage = null;

        var deletedTitle = SelectedPage.Title;
        var result = await _apiClient.DeletePageAsync(_userSession.SelectedWorkspace.Id, SelectedPage.Id);
        IsDeleting = false;

        if (result.IsSuccess)
        {
            SelectedPage = null;
            SelectedTreeNode = null;
            HasSelectedPage = false;
            await LoadPageTreeAsync();
            StatusMessage = $"Page '{deletedTitle}' and its subpages were deleted.";
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadPageTreeAsync();
    }

    private void PopulateParentOptions()
    {
        ParentPageOptions.Clear();
        ParentPageOptions.Add(new PageSummaryDto(Guid.Empty, Guid.Empty, null, "📁 Root (No Parent)", "📁", 0));

        void CollectFlattened(IReadOnlyList<PageTreeNodeDto> nodes, string prefix = "")
        {
            foreach (var node in nodes)
            {
                // Exclude current page from being its own parent in selection
                if (SelectedPage != null && node.Id == SelectedPage.Id) continue;

                ParentPageOptions.Add(new PageSummaryDto(node.Id, node.WorkspaceId, node.ParentPageId, $"{prefix}{node.Icon} {node.Title}", node.Icon, node.OrderIndex));
                if (node.Children.Count > 0)
                {
                    CollectFlattened(node.Children, prefix + "  └─ ");
                }
            }
        }

        CollectFlattened(PageTree);
    }

    private async Task SelectPageByIdAsync(Guid pageId)
    {
        PageTreeNodeDto? FindNode(IReadOnlyList<PageTreeNodeDto> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Id == pageId) return node;
                var found = FindNode(node.Children);
                if (found != null) return found;
            }
            return null;
        }

        var targetNode = FindNode(PageTree);
        if (targetNode != null)
        {
            await SelectPageAsync(targetNode);
        }
    }
}
