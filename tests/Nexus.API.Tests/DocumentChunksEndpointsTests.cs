using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Workspaces;
using Xunit;

namespace Nexus.API.Tests;

public class DocumentChunksEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentChunksEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<DocumentDto> UploadDocumentAsync(HttpClient client, Guid workspaceId, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", fileName);

        var response = await client.PostAsync($"/api/workspaces/{workspaceId}/documents", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    [Fact]
    public async Task ChunkDocument_Without_Authentication_Should_Return_401()
    {
        var response = await _client.PostAsync($"/api/workspaces/{Guid.NewGuid()}/documents/{Guid.NewGuid()}/chunk", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChunkDocument_ValidDocument_Should_Return_Chunks()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("chunk_valid");
        var doc = await UploadDocumentAsync(client, workspace.Id, "knowledge.txt", "Knowledge processing and chunking allows vector search to operate over small text blocks.");

        var response = await client.PostAsync($"/api/workspaces/{workspace.Id}/documents/{doc.Id}/chunk", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chunks = await response.Content.ReadFromJsonAsync<List<DocumentChunkDto>>();
        Assert.NotNull(chunks);
        Assert.NotEmpty(chunks);
        Assert.Equal(doc.Id, chunks[0].DocumentId);
    }

    [Fact]
    public async Task GetChunks_Should_Return_Persisted_Chunks()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("chunk_get");
        var doc = await UploadDocumentAsync(client, workspace.Id, "article.txt", "This is an article that will be chunked and retrieved via GET chunks endpoint.");

        await client.PostAsync($"/api/workspaces/{workspace.Id}/documents/{doc.Id}/chunk", null);

        var getResponse = await client.GetAsync($"/api/workspaces/{workspace.Id}/documents/{doc.Id}/chunks");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var chunks = await getResponse.Content.ReadFromJsonAsync<List<DocumentChunkDto>>();
        Assert.NotNull(chunks);
        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task ChunkDocument_Nonexistent_Document_Should_Return_404()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("chunk_404");
        var response = await client.PostAsync($"/api/workspaces/{workspace.Id}/documents/{Guid.NewGuid()}/chunk", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
