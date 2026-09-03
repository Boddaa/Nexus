using System.Windows;
using System.Windows.Controls;
using Nexus.Desktop.ViewModels;

namespace Nexus.Desktop.Views;

public partial class WorkspaceSelectorView : UserControl
{
    public WorkspaceSelectorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceSelectorViewModel vm)
        {
            await vm.LoadWorkspacesAsync();
        }
    }
}
