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
    private readonly IDialogService _dialogService;
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
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isDeleting;

    public bool IsBusy => IsLoading || IsSaving || IsDeleting;

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

    public PagesViewModel(IApiClient apiClient, IDialogService dialogService, UserSession userSession)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _userSession = userSession;

        if (_userSession.SelectedWorkspace != null)
        {
            LoadPageTreeCommand.Execute(null);
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

        try
        {
            var result = await _apiClient.GetPageTreeAsync(_userSession.SelectedWorkspace.Id);

            if (result.IsSuccess)
            {
                PageTree.Clear();
                foreach (var node in result.Value)
                {
                    PageTree.Add(node);
                }
                PopulateCreateParentOptions();
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
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

        try
        {
            var result = await _apiClient.GetPageByIdAsync(_userSession.SelectedWorkspace.Id, node.Id);

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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenCreateDialog(string? asChildParam)
    {
        if (IsBusy) return;

        bool asChild = asChildParam == "child";
        NewPageTitle = "Untitled Page";
        NewPageIcon = "📄";
        NewPageOrderIndex = 0;
        NewPageParentId = asChild && SelectedPage != null ? SelectedPage.Id : null;

        PopulateCreateParentOptions();
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
        if (_userSession.SelectedWorkspace == null || IsBusy) return;

        if (string.IsNullOrWhiteSpace(NewPageTitle))
        {
            ErrorMessage = "Page title cannot be empty.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var request = new CreatePageRequest(
                NewPageTitle.Trim(),
                string.IsNullOrWhiteSpace(NewPageIcon) ? "📄" : NewPageIcon.Trim(),
                null,
                "{}",
                NewPageParentId,
                NewPageOrderIndex);

            var result = await _apiClient.CreatePageAsync(_userSession.SelectedWorkspace.Id, request);

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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SavePageAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedPage == null || IsBusy) return;

        if (string.IsNullOrWhiteSpace(EditorTitle))
        {
            ErrorMessage = "Page title cannot be empty.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var request = new UpdatePageRequest(
                EditorTitle.Trim(),
                string.IsNullOrWhiteSpace(EditorIcon) ? "📄" : EditorIcon.Trim(),
                EditorCoverImageUrl?.Trim(),
                EditorContentJson ?? "{}",
                EditorOrderIndex);

            var result = await _apiClient.UpdatePageAsync(_userSession.SelectedWorkspace.Id, SelectedPage.Id, request);

            if (result.IsSuccess)
            {
                SelectedPage = result.Value;
                EditorTitle = result.Value.Title;
                EditorIcon = result.Value.Icon;
                EditorContentJson = result.Value.ContentJson;
                EditorOrderIndex = result.Value.OrderIndex;
                EditorCoverImageUrl = result.Value.CoverImageUrl;
                ChildPagesCount = result.Value.ChildPagesCount;
                NotesCount = result.Value.NotesCount;

                await LoadPageTreeAsync();
                SelectedTreeNode = FindNode(PageTree, result.Value.Id);
                HasSelectedPage = true;
                StatusMessage = "Page saved successfully.";
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public void OpenMoveDialog()
    {
        if (SelectedPage == null || IsBusy) return;

        MoveTargetParentId = SelectedPage.ParentPageId;
        MoveNewOrderIndex = SelectedPage.OrderIndex;
        PopulateMoveParentOptions();
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
        if (_userSession.SelectedWorkspace == null || SelectedPage == null || IsBusy) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var targetPageId = SelectedPage.Id;
            var request = new MovePageRequest(MoveTargetParentId, MoveNewOrderIndex);
            var result = await _apiClient.MovePageAsync(_userSession.SelectedWorkspace.Id, targetPageId, request);

            if (result.IsSuccess)
            {
                IsMoveDialogOpen = false;
                await LoadPageTreeAsync();
                await SelectPageByIdAsync(targetPageId);
                StatusMessage = "Page moved successfully.";
            }
            else
            {
                ErrorMessage = result.Error.Description;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeletePageAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedPage == null || IsBusy) return;

        bool confirmed = await _dialogService.ConfirmAsync(
            "Delete Page",
            $"Are you sure you want to delete page '{SelectedPage.Title}'?\n\nThis will permanently delete this page and all of its subpages.");

        if (!confirmed) return;

        IsDeleting = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var deletedTitle = SelectedPage.Title;
            var result = await _apiClient.DeletePageAsync(_userSession.SelectedWorkspace.Id, SelectedPage.Id);

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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsDeleting = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        await LoadPageTreeAsync();
    }

    private void PopulateCreateParentOptions()
    {
        ParentPageOptions.Clear();
        ParentPageOptions.Add(new PageSummaryDto(Guid.Empty, Guid.Empty, null, "📁 Root (No Parent)", "📁", 0));

        void CollectFlattened(IReadOnlyList<PageTreeNodeDto> nodes, string prefix = "")
        {
            foreach (var node in nodes)
            {
                ParentPageOptions.Add(new PageSummaryDto(node.Id, node.WorkspaceId, node.ParentPageId, $"{prefix}{node.Icon} {node.Title}", node.Icon, node.OrderIndex));
                if (node.Children.Count > 0)
                {
                    CollectFlattened(node.Children, prefix + "  └─ ");
                }
            }
        }

        CollectFlattened(PageTree);
    }

    private void PopulateMoveParentOptions()
    {
        ParentPageOptions.Clear();
        ParentPageOptions.Add(new PageSummaryDto(Guid.Empty, Guid.Empty, null, "📁 Root (No Parent)", "📁", 0));

        var excludedIds = SelectedPage != null ? GetDescendantsAndSelf(SelectedPage.Id) : new HashSet<Guid>();

        void CollectValidNodes(IReadOnlyList<PageTreeNodeDto> nodes, string prefix = "")
        {
            foreach (var node in nodes)
            {
                if (excludedIds.Contains(node.Id)) continue; // Exclude self and all descendants!

                ParentPageOptions.Add(new PageSummaryDto(
                    node.Id,
                    node.WorkspaceId,
                    node.ParentPageId,
                    $"{prefix}{node.Icon} {node.Title}",
                    node.Icon,
                    node.OrderIndex));

                if (node.Children.Count > 0)
                {
                    CollectValidNodes(node.Children, prefix + "  └─ ");
                }
            }
        }

        CollectValidNodes(PageTree);
    }

    public HashSet<Guid> GetDescendantsAndSelf(Guid rootId)
    {
        var set = new HashSet<Guid> { rootId };
        var node = FindNode(PageTree, rootId);
        if (node != null)
        {
            void AddSubtree(PageTreeNodeDto parent)
            {
                foreach (var child in parent.Children)
                {
                    set.Add(child.Id);
                    AddSubtree(child);
                }
            }
            AddSubtree(node);
        }
        return set;
    }

    private static PageTreeNodeDto? FindNode(IReadOnlyList<PageTreeNodeDto> nodes, Guid pageId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == pageId) return node;
            var found = FindNode(node.Children, pageId);
            if (found != null) return found;
        }
        return null;
    }

    public async Task SelectPageByIdAsync(Guid pageId)
    {
        if (PageTree.Count == 0 && _userSession.SelectedWorkspace != null)
        {
            await LoadPageTreeAsync();
        }

        var targetNode = FindNode(PageTree, pageId);
        if (targetNode != null)
        {
            await SelectPageAsync(targetNode);
        }
        else if (_userSession.SelectedWorkspace != null)
        {
            var result = await _apiClient.GetPageByIdAsync(_userSession.SelectedWorkspace.Id, pageId);
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
        }
    }
}
