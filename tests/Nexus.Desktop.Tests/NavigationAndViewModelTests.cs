using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.DTOs.Auth;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Xunit;

namespace Nexus.Desktop.Tests;

public class NavigationAndViewModelTests
{
    [Fact]
    public void NavigationService_Should_Navigate_And_Notify_Subscribers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<UserSession>();
        services.AddSingleton<ITokenStorage, TokenStorage>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IApiClient, FakeApiClient>();
        services.AddSingleton<IDialogService, FakeDialogService>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<PagesViewModel>();
        services.AddTransient<NotesViewModel>();

        var provider = services.BuildServiceProvider();
        var navService = provider.GetRequiredService<INavigationService>();

        bool eventFired = false;
        navService.CurrentViewModelChanged += () => eventFired = true;

        // Act 1 - Home
        navService.NavigateTo<HomeViewModel>();

        // Assert 1
        Assert.True(eventFired);
        Assert.IsType<HomeViewModel>(navService.CurrentViewModel);

        // Act 2 - Pages
        eventFired = false;
        navService.NavigateTo<PagesViewModel>();

        // Assert 2
        Assert.True(eventFired);
        Assert.IsType<PagesViewModel>(navService.CurrentViewModel);

        // Act 3 - Notes
        eventFired = false;
        navService.NavigateTo<NotesViewModel>();

        // Assert 3
        Assert.True(eventFired);
        Assert.IsType<NotesViewModel>(navService.CurrentViewModel);
    }

    [Fact]
    public void UserSession_Should_Reflect_Authentication_State()
    {
        // Arrange
        var session = new UserSession();
        Assert.False(session.IsAuthenticated);

        // Act
        session.CurrentUser = new AuthResponse(
            Guid.NewGuid(),
            "user@nexus.ai",
            "Test User",
            "User",
            "mock.jwt.token",
            DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.True(session.IsAuthenticated);
    }
}
