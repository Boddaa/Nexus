using Nexus.Desktop.Services;

namespace Nexus.Desktop.Tests.Fakes;

public class FakeDialogService : IDialogService
{
    public bool ConfirmationResult { get; set; } = true;
    public int ConfirmCallCount { get; private set; }
    public string? LastConfirmTitle { get; private set; }
    public string? LastConfirmMessage { get; private set; }

    public int ShowMessageCallCount { get; private set; }
    public string? LastMessageTitle { get; private set; }
    public string? LastMessageText { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message, string confirmButtonText = "Delete", string cancelButtonText = "Cancel")
    {
        ConfirmCallCount++;
        LastConfirmTitle = title;
        LastConfirmMessage = message;
        return Task.FromResult(ConfirmationResult);
    }

    public void ShowMessage(string title, string message)
    {
        ShowMessageCallCount++;
        LastMessageTitle = title;
        LastMessageText = message;
    }
}
