namespace Nexus.Desktop.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmButtonText = "Delete", string cancelButtonText = "Cancel");
    void ShowMessage(string title, string message);
}

public class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmButtonText = "Delete", string cancelButtonText = "Cancel")
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        return Task.FromResult(result == System.Windows.MessageBoxResult.Yes);
    }

    public void ShowMessage(string title, string message)
    {
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
