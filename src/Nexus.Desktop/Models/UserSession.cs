using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Workspaces;

namespace Nexus.Desktop.Models;

public class UserSession
{
    public AuthResponse? CurrentUser { get; set; }
    public WorkspaceDto? SelectedWorkspace { get; set; }
    public Guid CurrentWorkspaceId => SelectedWorkspace?.Id ?? Guid.Empty;
    public bool IsAuthenticated => CurrentUser != null && !string.IsNullOrWhiteSpace(CurrentUser.Token);
}
