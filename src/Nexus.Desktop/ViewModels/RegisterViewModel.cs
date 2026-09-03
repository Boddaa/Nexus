using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.Auth;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly INavigationService _navigationService;
    private readonly UserSession _userSession;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public RegisterViewModel(
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
    private async Task RegisterAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "All fields are required.";
            return;
        }

        IsLoading = true;
        var result = await _apiClient.RegisterAsync(new RegisterRequest(Email, Password, FullName));
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
    private void GoToLogin()
    {
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
