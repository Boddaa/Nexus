using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class WorkspaceSelectorViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private ObservableCollection<WorkspaceSummaryDto> _workspaces = new();

    [ObservableProperty]
    private WorkspaceSummaryDto? _selectedWorkspaceSummary;

    [ObservableProperty]
    private string _newWorkspaceName = string.Empty;

    [ObservableProperty]
    private string _newWorkspaceDescription = string.Empty;

    [ObservableProperty]
    private string _newWorkspaceIcon = "🧠";

    [ObservableProperty]
    private bool _isCreatingWorkspace;

    public WorkspaceSelectorViewModel(
        IApiClient apiClient,
        INavigationService navigationService,
        UserSession userSession)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;
        _userSession = userSession;
    }

    [RelayCommand]
    public async Task LoadWorkspacesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        var result = await _apiClient.GetUserWorkspacesAsync();
        IsLoading = false;

        if (result.IsSuccess)
        {
            Workspaces.Clear();
            foreach (var ws in result.Value)
            {
                Workspaces.Add(ws);
            }

            if (Workspaces.Count == 1)
            {
                // Auto-select if only one
                await SelectWorkspaceAsync(Workspaces[0]);
            }
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    public async Task SelectWorkspaceAsync(WorkspaceSummaryDto? summary)
    {
        if (summary is null) return;

        IsLoading = true;
        var result = await _apiClient.GetWorkspaceByIdAsync(summary.Id);
        IsLoading = false;

        if (result.IsSuccess)
        {
            _userSession.SelectedWorkspace = result.Value;
            _navigationService.NavigateTo<HomeViewModel>();
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    private void ToggleCreateMode()
    {
        IsCreatingWorkspace = !IsCreatingWorkspace;
    }

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWorkspaceName))
        {
            ErrorMessage = "Workspace name is required.";
            return;
        }

        IsLoading = true;
        var result = await _apiClient.CreateWorkspaceAsync(new CreateWorkspaceRequest(
            NewWorkspaceName,
            NewWorkspaceDescription,
            NewWorkspaceIcon,
            "#6366F1"));
        IsLoading = false;

        if (result.IsSuccess)
        {
            IsCreatingWorkspace = false;
            NewWorkspaceName = string.Empty;
            NewWorkspaceDescription = string.Empty;
            _userSession.SelectedWorkspace = result.Value;
            _navigationService.NavigateTo<HomeViewModel>();
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }
}
