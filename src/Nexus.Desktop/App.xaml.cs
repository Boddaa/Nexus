using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;
using Nexus.Desktop.ViewModels;

namespace Nexus.Desktop;

public partial class App : System.Windows.Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // HTTP Client & API Service
        services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5000/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // State, Navigation & Dialog Services
        services.AddSingleton<UserSession>();
        services.AddSingleton<ITokenStorage, TokenStorage>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<WorkspaceSelectorViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<PagesViewModel>();
        services.AddTransient<NotesViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<BoardsViewModel>();
        services.AddTransient<MindMapsViewModel>();
        services.AddTransient<StudyViewModel>();
        services.AddTransient<AiOperationViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<AiAssistantViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }
}
