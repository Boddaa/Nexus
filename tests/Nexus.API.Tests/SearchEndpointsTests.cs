using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Search;
using Nexus.Application.DTOs.Workspaces;
using Xunit;

namespace Nexus.API.Tests;

public class SearchEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SearchEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient authenticatedClient, AuthResponse user, WorkspaceSummaryDto defaultWorkspace)> CreateUserAndWorkspaceAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@nexus.ai";
        var password = "SecurePassword123!";
        var registerRequest = new RegisterRequest(email, password, $"{prefix} User");

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        regResponse.EnsureSuccessStatusCode();

        var authData = (await regResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var requestClient = _factory.CreateClient();
        requestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);

        var wsResponse = await requestClient.GetAsync("/api/workspaces");
        wsResponse.EnsureSuccessStatusCode();
        var workspaces = (await wsResponse.Content.ReadFromJsonAsync<List<WorkspaceSummaryDto>>())!;

        return (requestClient, authData, workspaces.First());
    }

    [Fact]
    public async Task Search_Without_Authentication_Should_Return_401()
    {
        var response = await _client.GetAsync($"/api/workspaces/{Guid.NewGuid()}/search?q=test");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_Empty_Query_Should_Return_400()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("search_empty");
        var response = await client.GetAsync($"/api/workspaces/{workspace.Id}/search?q=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_Invalid_Type_Should_Return_400()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("search_invalid_type");
        var response = await client.GetAsync($"/api/workspaces/{workspace.Id}/search?q=test&type=InvalidType");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_Should_Return_Unified_Matches_Across_Pages_Notes_And_Documents()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("search_unified");
        var wsId = workspace.Id;

        // 1. Create Page
        var pageReq = new CreatePageRequest("Postgres Database Design", "🐘", null, "{\"text\":\"Postgres replication cluster setup.\"}", null, 0);
        var pageRes = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", pageReq);
        Assert.Equal(HttpStatusCode.Created, pageRes.StatusCode);

        // 2. Create Note
        var noteReq = new CreateNoteRequest("Database Queries", "Postgres index performance tuning", "markdown", false, null, new List<string> { "Postgres" });
        var noteRes = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/notes", noteReq);
        Assert.Equal(HttpStatusCode.Created, noteRes.StatusCode);

        // 3. Upload Document
        using var formData = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes("Postgres backup and restore procedures.");
        var fileStreamContent = new ByteArrayContent(fileBytes);
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formData.Add(fileStreamContent, "file", "postgres_backup.txt");
        formData.Add(new StringContent("Postgres Backup Guide"), "title");

        var docRes = await client.PostAsync($"/api/workspaces/{wsId}/documents", formData);
        Assert.Equal(HttpStatusCode.Created, docRes.StatusCode);

        // 4. Search for "Postgres"
        var searchRes = await client.GetAsync($"/api/workspaces/{wsId}/search?q=Postgres");
        Assert.Equal(HttpStatusCode.OK, searchRes.StatusCode);

        var pagedResult = await searchRes.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(pagedResult);
        Assert.Equal(3, pagedResult.TotalCount);
        Assert.Contains(pagedResult.Items, i => i.Type == "Page" && i.Title == "Postgres Database Design");
        Assert.Contains(pagedResult.Items, i => i.Type == "Note" && i.Title == "Database Queries");
        Assert.Contains(pagedResult.Items, i => i.Type == "Document" && i.Title == "Postgres Backup Guide");
    }

    [Fact]
    public async Task Search_With_Type_Filter_Should_Only_Return_Specified_Type()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("search_type_filter");
        var wsId = workspace.Id;

        // Create Note
        var noteReq = new CreateNoteRequest("Redis Caching", "In-memory database", "markdown", false, null, null);
        await client.PostAsJsonAsync($"/api/workspaces/{wsId}/notes", noteReq);

        // Create Page
        var pageReq = new CreatePageRequest("Redis Clustering", "🔴", null, "{}", null, 0);
        await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", pageReq);

        // Search with type=Note
        var searchRes = await client.GetAsync($"/api/workspaces/{wsId}/search?q=Redis&type=Note");
        Assert.Equal(HttpStatusCode.OK, searchRes.StatusCode);

        var result = await searchRes.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("Note", result.Items[0].Type);
        Assert.Equal("Redis Caching", result.Items[0].Title);
    }

    [Fact]
    public async Task Search_Workspace_Isolation_Should_Not_Return_Foreign_Workspace_Items()
    {
        var (clientA, _, workspaceA) = await CreateUserAndWorkspaceAsync("user_search_a");
        var (clientB, _, workspaceB) = await CreateUserAndWorkspaceAsync("user_search_b");

        // Note in Workspace A
        var noteReqA = new CreateNoteRequest("Secret Token Alpha", "A confidential info", "markdown", false, null, null);
        await clientA.PostAsJsonAsync($"/api/workspaces/{workspaceA.Id}/notes", noteReqA);

        // Note in Workspace B
        var noteReqB = new CreateNoteRequest("Secret Token Beta", "B confidential info", "markdown", false, null, null);
        await clientB.PostAsJsonAsync($"/api/workspaces/{workspaceB.Id}/notes", noteReqB);

        // User A searches Workspace A
        var searchResA = await clientA.GetAsync($"/api/workspaces/{workspaceA.Id}/search?q=Secret%20Token");
        Assert.Equal(HttpStatusCode.OK, searchResA.StatusCode);
        var resA = await searchResA.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>();
        Assert.Single(resA!.Items);
        Assert.Equal("Secret Token Alpha", resA.Items[0].Title);

        // User A attempts to search Workspace B -> Unauthorized
        var unauthorizedRes = await clientA.GetAsync($"/api/workspaces/{workspaceB.Id}/search?q=Secret");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedRes.StatusCode);
    }

    [Fact]
    public async Task Search_With_Invalid_Mode_Should_Return_400()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("search_invalid_mode");
        var response = await client.GetAsync($"/api/workspaces/{workspace.Id}/search?q=test&mode=UnknownMode");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_With_Keyword_Mode_Should_Return_200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("search_kw_mode");
        var response = await client.GetAsync($"/api/workspaces/{workspace.Id}/search?q=test&mode=Keyword");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
