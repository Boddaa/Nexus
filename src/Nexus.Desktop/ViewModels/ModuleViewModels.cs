using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.Common.Models;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly UserSession _userSession;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _workspaceTitle = "Workspace";

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to NEXUS";

    [ObservableProperty]
    private int _notesCount;

    [ObservableProperty]
    private int _documentsCount;

    [ObservableProperty]
    private int _tasksCount;

    public HomeViewModel(UserSession userSession, INavigationService navigationService)
    {
        _userSession = userSession;
        _navigationService = navigationService;

        var ws = _userSession.SelectedWorkspace;
        if (ws != null)
        {
            WorkspaceTitle = $"{ws.Icon} {ws.Name}";
            WelcomeMessage = $"Welcome back, {_userSession.CurrentUser?.FullName ?? "Explorer"}!";
            NotesCount = ws.NotesCount;
            DocumentsCount = ws.DocumentsCount;
            TasksCount = ws.TasksCount;
        }
    }

    [RelayCommand]
    private void GoToNotes() => _navigationService.NavigateTo<NotesViewModel>();

    [RelayCommand]
    private void GoToDocuments() => _navigationService.NavigateTo<DocumentsViewModel>();

    [RelayCommand]
    private void GoToBoards() => _navigationService.NavigateTo<BoardsViewModel>();

    [RelayCommand]
    private void GoToMindMaps() => _navigationService.NavigateTo<MindMapsViewModel>();

    [RelayCommand]
    private void GoToStudy() => _navigationService.NavigateTo<StudyViewModel>();
}

public partial class BoardsViewModel : ViewModelBase
{
    private readonly UserSession _userSession;

    [ObservableProperty]
    private string _boardTitle = "EF Core Study Board";

    public BoardsViewModel(UserSession userSession)
    {
        _userSession = userSession;
    }
}

public partial class MindMapsViewModel : ViewModelBase
{
    private readonly UserSession _userSession;

    [ObservableProperty]
    private string _mindMapTitle = "Knowledge Graph & Mind Map";

    public MindMapsViewModel(UserSession userSession)
    {
        _userSession = userSession;
    }
}

public partial class StudyViewModel : AiOperationViewModel
{
    public StudyViewModel(
        IApiClient apiClient,
        IDialogService dialogService,
        INavigationService navigationService,
        UserSession userSession)
        : base(apiClient, dialogService, navigationService, userSession)
    {
    }
}

