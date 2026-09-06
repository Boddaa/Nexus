using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Pages;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly IFilePickerService _filePickerService;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private ObservableCollection<DocumentSummaryDto> _documents = new();

    [ObservableProperty]
    private ObservableCollection<DocumentSummaryDto> _filteredDocuments = new();

    [ObservableProperty]
    private DocumentSummaryDto? _selectedDocumentSummary;

    [ObservableProperty]
    private DocumentDetailDto? _selectedDocument;

    [ObservableProperty]
    private bool _hasSelectedDocument;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Guid? _selectedPageFilterId;

    [ObservableProperty]
    private ObservableCollection<PageSummaryDto> _pageFilterOptions = new();

    // Async state flags
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isUploading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isDeleting;

    public bool IsBusy => IsLoading || IsUploading || IsDeleting;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    private readonly INavigationService? _navigationService;

    public DocumentsViewModel(
        IApiClient apiClient,
        IDialogService dialogService,
        IFilePickerService filePickerService,
        UserSession userSession,
        INavigationService? navigationService = null)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
        _userSession = userSession;
        _navigationService = navigationService;

        if (_userSession.SelectedWorkspace != null)
        {
            InitializeSafe();
        }
    }

    private void InitializeSafe()
    {
        _ = LoadPageFilterOptionsAsync();
        _ = LoadDocumentsAsync();
    }

    [RelayCommand]
    public async Task LoadPageFilterOptionsAsync()
    {
        if (_userSession.SelectedWorkspace == null) return;

        try
        {
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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task LoadDocumentsAsync()
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

        try
        {
            var result = await _apiClient.GetDocumentsAsync(_userSession.SelectedWorkspace.Id, pageIdParam);

            if (result.IsSuccess)
            {
                Documents.Clear();
                foreach (var doc in result.Value)
                {
                    Documents.Add(doc);
                }
                ApplyLocalFilter();
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
    public async Task SelectDocumentAsync(DocumentSummaryDto? summary)
    {
        if (summary == null || _userSession.SelectedWorkspace == null)
        {
            SelectedDocumentSummary = null;
            SelectedDocument = null;
            HasSelectedDocument = false;
            return;
        }

        SelectedDocumentSummary = summary;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetDocumentByIdAsync(_userSession.SelectedWorkspace.Id, summary.Id);

            if (result.IsSuccess)
            {
                SelectedDocument = result.Value;
                HasSelectedDocument = true;
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
    public async Task UploadDocumentAsync()
    {
        if (_userSession.SelectedWorkspace == null || IsBusy) return;

        var picked = _filePickerService.PickFileForOpen();
        if (picked == null) return;

        IsUploading = true;
        ErrorMessage = null;
        StatusMessage = null;

        Guid? pageIdParam = SelectedPageFilterId.HasValue && SelectedPageFilterId.Value != Guid.Empty
            ? SelectedPageFilterId.Value
            : null;

        try
        {
            using var stream = picked.OpenRead();
            var result = await _apiClient.UploadDocumentAsync(
                _userSession.SelectedWorkspace.Id,
                stream,
                picked.FileName,
                picked.ContentType,
                title: null,
                pageId: pageIdParam);

            if (result.IsSuccess)
            {
                StatusMessage = $"Document '{result.Value.FileName}' uploaded and parsed successfully ({result.Value.ExtractedTextLength} characters extracted).";
                await LoadDocumentsAsync();

                // Select uploaded document
                var summary = Documents.FirstOrDefault(d => d.Id == result.Value.Id);
                if (summary != null)
                {
                    await SelectDocumentAsync(summary);
                }
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
            IsUploading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteDocumentAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedDocument == null || IsBusy) return;

        bool confirmed = await _dialogService.ConfirmAsync(
            "Delete Document",
            $"Are you sure you want to delete document '{SelectedDocument.FileName}'?\n\nThis will permanently delete the physical file and extracted text.");

        if (!confirmed) return;

        IsDeleting = true;
        ErrorMessage = null;
        StatusMessage = null;

        var deletedName = SelectedDocument.FileName;
        var docId = SelectedDocument.Id;

        try
        {
            var result = await _apiClient.DeleteDocumentAsync(_userSession.SelectedWorkspace.Id, docId);

            if (result.IsSuccess)
            {
                SelectedDocument = null;
                SelectedDocumentSummary = null;
                HasSelectedDocument = false;
                await LoadDocumentsAsync();
                StatusMessage = $"Document '{deletedName}' was deleted.";
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
    public async Task DownloadDocumentAsync()
    {
        if (_userSession.SelectedWorkspace == null || SelectedDocument == null || IsBusy) return;

        var savePath = _filePickerService.PickFileForSave(SelectedDocument.FileName);
        if (string.IsNullOrWhiteSpace(savePath)) return;

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var result = await _apiClient.DownloadDocumentAsync(_userSession.SelectedWorkspace.Id, SelectedDocument.Id);

            if (result.IsSuccess)
            {
                await File.WriteAllBytesAsync(savePath, result.Value);
                StatusMessage = $"Document downloaded successfully to '{Path.GetFileName(savePath)}'.";
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
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        await LoadDocumentsAsync();
    }

    [RelayCommand]
    public void ApplyLocalFilter()
    {
        FilteredDocuments.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var doc in Documents)
            {
                FilteredDocuments.Add(doc);
            }
            return;
        }

        var term = SearchText.Trim();
        foreach (var doc in Documents)
        {
            if (doc.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                doc.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                doc.FileExtension.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (doc.PageTitle != null && doc.PageTitle.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredDocuments.Add(doc);
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyLocalFilter();
    }

    partial void OnSelectedPageFilterIdChanged(Guid? value)
    {
        _ = LoadDocumentsAsync();
    }

    public async Task SelectDocumentByIdAsync(Guid documentId)
    {
        if (Documents.Count == 0 && _userSession.SelectedWorkspace != null)
        {
            await LoadDocumentsAsync();
        }

        var summary = Documents.FirstOrDefault(d => d.Id == documentId);
        if (summary != null)
        {
            await SelectDocumentAsync(summary);
        }
        else if (_userSession.SelectedWorkspace != null)
        {
            var result = await _apiClient.GetDocumentByIdAsync(_userSession.SelectedWorkspace.Id, documentId);
            if (result.IsSuccess)
            {
                SelectedDocument = result.Value;
                HasSelectedDocument = true;
            }
        }
    }

    [RelayCommand]
    public void RunAiOperationOnDocument(string operation)
    {
        if (SelectedDocument == null) return;
        _userSession.PendingAiTarget = new AiWorkflowTarget(
            "Document",
            SelectedDocument.Id,
            SelectedDocument.FileName,
            operation);

        _navigationService?.NavigateTo<StudyViewModel>();
    }
}
