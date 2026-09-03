using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Desktop.Services;

namespace Nexus.Desktop.Tests.Fakes;

public class FakeNavigationService : INavigationService
{
    public ObservableObject? CurrentViewModel { get; set; }
    public Type? LastNavigatedType { get; private set; }
    public int NavigationCount { get; private set; }
    public Func<Type, ObservableObject?>? ViewModelResolver { get; set; }
    public event Action? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        LastNavigatedType = typeof(TViewModel);
        NavigationCount++;
        if (ViewModelResolver != null)
        {
            CurrentViewModel = ViewModelResolver(typeof(TViewModel));
        }
        CurrentViewModelChanged?.Invoke();
    }
}
