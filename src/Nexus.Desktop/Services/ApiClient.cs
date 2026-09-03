using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Common;

namespace Nexus.Desktop.Services;

public interface IApiClient
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    // Pages
    Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default);

    // Notes
    Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    private void SetAuthorizationHeader()
    {
        var token = _tokenStorage.GetToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    #region Auth & Workspace Methods

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<AuthResponse>(Error.NullValue);
            }

            return await ExtractErrorAsync<AuthResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<AuthResponse>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<AuthResponse>(Error.NullValue);
            }

            return await ExtractErrorAsync<AuthResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<AuthResponse>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync("api/auth/me", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<UserDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<UserDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<UserDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync("api/workspaces", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkspaceSummaryDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<WorkspaceSummaryDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<WorkspaceDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<WorkspaceDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkspaceDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/workspaces", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<WorkspaceDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<WorkspaceDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkspaceDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<WorkspaceDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<WorkspaceDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkspaceDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    #region Pages Methods

    public async Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/pages", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<PageTreeNodeDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<PageTreeNodeDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<PageTreeNodeDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<PageTreeNodeDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/pages/{pageId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/pages", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/pages/{pageId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/pages/{pageId}/move", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/pages/{pageId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    #region Notes Methods

    public async Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var queryParams = new List<string>();
            if (pageId.HasValue)
            {
                queryParams.Add($"pageId={pageId.Value}");
            }
            if (isPinned.HasValue)
            {
                queryParams.Add($"isPinned={isPinned.Value.ToString().ToLowerInvariant()}");
            }

            var url = $"api/workspaces/{workspaceId}/notes";
            if (queryParams.Count > 0)
            {
                url += "?" + string.Join("&", queryParams);
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<NoteSummaryDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<NoteSummaryDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<NoteSummaryDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<NoteSummaryDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/notes/{noteId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<NoteDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/notes", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<NoteDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/notes/{noteId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<NoteDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/notes/{noteId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    private async Task<Result<T>> ExtractErrorAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var errorDoc = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions, cancellationToken);
            if (errorDoc.TryGetProperty("description", out var desc) || errorDoc.TryGetProperty("Description", out desc))
            {
                var code = errorDoc.TryGetProperty("code", out var c) || errorDoc.TryGetProperty("Code", out c) ? c.GetString() : "API.Error";
                return Result.Failure<T>(new Error(code ?? "API.Error", desc.GetString() ?? "Request failed."));
            }
        }
        catch
        {
            // Ignore JSON parse errors on non-standard error bodies
        }

        return Result.Failure<T>(new Error("API.Error", $"Request failed with status code {response.StatusCode}"));
    }

    private async Task<Result> ExtractErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var res = await ExtractErrorAsync<object>(response, cancellationToken);
        return Result.Failure(res.Error);
    }
}
