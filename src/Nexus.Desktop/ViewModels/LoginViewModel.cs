using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Auth;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly INavigationService _navigationService;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private string _email = "admin@nexus.ai";

    [ObservableProperty]
    private string _password = "Password123!";

    public LoginViewModel(
        IApiClient apiClient,
        ITokenStorage tokenStorage,
        INavigationService navigationService,
        UserSession userSession)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _navigationService = navigationService;
        _userSession = userSession;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return;
        }

        IsLoading = true;
        var result = await _apiClient.LoginAsync(new LoginRequest(Email, Password));
        IsLoading = false;

        if (result.IsSuccess)
        {
            _userSession.CurrentUser = result.Value;
            _tokenStorage.SaveToken(result.Value.Token);
            _navigationService.NavigateTo<WorkspaceSelectorViewModel>();
        }
        else
        {
            ErrorMessage = result.Error.Description;
        }
    }

    [RelayCommand]
    private void GoToRegister()
    {
        _navigationService.NavigateTo<RegisterViewModel>();
    }
}
