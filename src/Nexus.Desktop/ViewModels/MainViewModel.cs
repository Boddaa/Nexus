using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ITokenStorage _tokenStorage;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private bool _isAiPanelOpen = true;

    [ObservableProperty]
    private string _currentWorkspaceName = "Select Workspace";

    [ObservableProperty]
    private string _currentWorkspaceIcon = "📁";

    [ObservableProperty]
    private string _userFullName = "Guest";

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private bool _isAuthenticated;

    public MainViewModel(
        INavigationService navigationService,
        ITokenStorage tokenStorage,
        UserSession userSession)
    {
        _navigationService = navigationService;
        _tokenStorage = tokenStorage;
        _userSession = userSession;

        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;

        // Start at LoginView
        _navigationService.NavigateTo<LoginViewModel>();
    }

    private void OnCurrentViewModelChanged()
    {
        CurrentView = _navigationService.CurrentViewModel;

        IsAuthenticated = _userSession.IsAuthenticated;
        UserFullName = _userSession.CurrentUser?.FullName ?? "Guest";
        UserEmail = _userSession.CurrentUser?.Email ?? string.Empty;

        if (_userSession.SelectedWorkspace != null)
        {
            CurrentWorkspaceName = _userSession.SelectedWorkspace.Name;
            CurrentWorkspaceIcon = _userSession.SelectedWorkspace.Icon;
        }
        else
        {
            CurrentWorkspaceName = "No Workspace Selected";
            CurrentWorkspaceIcon = "📁";
        }
    }

    [RelayCommand]
    private void NavigateHome() => _navigationService.NavigateTo<HomeViewModel>();

    [RelayCommand]
    private void NavigatePages() => _navigationService.NavigateTo<PagesViewModel>();

    [RelayCommand]
    private void NavigateNotes() => _navigationService.NavigateTo<NotesViewModel>();

    [RelayCommand]
    private void NavigateDocuments() => _navigationService.NavigateTo<DocumentsViewModel>();

    [RelayCommand]
    private void NavigateBoards() => _navigationService.NavigateTo<BoardsViewModel>();

    [RelayCommand]
    private void NavigateMindMaps() => _navigationService.NavigateTo<MindMapsViewModel>();

    [RelayCommand]
    private void NavigateStudy() => _navigationService.NavigateTo<StudyViewModel>();

    [RelayCommand]
    private void NavigateSearch() => _navigationService.NavigateTo<SearchViewModel>();

    [RelayCommand]
    private void SwitchWorkspace() => _navigationService.NavigateTo<WorkspaceSelectorViewModel>();

    [RelayCommand]
    private void ToggleAiPanel() => IsAiPanelOpen = !IsAiPanelOpen;

    [RelayCommand]
    private void Logout()
    {
        _tokenStorage.ClearToken();
        _userSession.CurrentUser = null;
        _userSession.SelectedWorkspace = null;
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
