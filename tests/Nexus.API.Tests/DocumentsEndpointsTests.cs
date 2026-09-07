using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Workspaces;
using Xunit;

namespace Nexus.API.Tests;

public class DocumentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentsEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task Upload_Without_Authentication_Should_Return_401()
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent("test"), "file", "test.txt");

        var response = await _client.PostAsync($"/api/workspaces/{Guid.NewGuid()}/documents", formData);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Documents_Upload_Retrieve_Download_Delete_Flow_Should_Succeed()
    {
        // 1. Authenticate user
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("doc_flow");
        var wsId = workspace.Id;

        // 2. Upload Document
        var fileContent = "This is a document for knowledge parsing and semantic search.";
        using var formData = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var fileStreamContent = new ByteArrayContent(fileBytes);
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formData.Add(fileStreamContent, "file", "spec.txt");
        formData.Add(new StringContent("Architecture Spec"), "title");

        var uploadResponse = await client.PostAsync($"/api/workspaces/{wsId}/documents", formData);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var doc = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(doc);
        Assert.Equal("Architecture Spec", doc.Title);
        Assert.Equal("spec.txt", doc.FileName);
        Assert.Equal(".txt", doc.FileExtension);
        Assert.Equal(fileContent.Length, doc.ExtractedTextLength);

        // 3. List Documents
        var listResponse = await client.GetAsync($"/api/workspaces/{wsId}/documents");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var docList = await listResponse.Content.ReadFromJsonAsync<List<DocumentSummaryDto>>();
        Assert.NotNull(docList);
        Assert.Single(docList);
        Assert.Equal(doc.Id, docList[0].Id);

        // 4. Get Document Details by ID
        var detailResponse = await client.GetAsync($"/api/workspaces/{wsId}/documents/{doc.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<DocumentDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(fileContent, detail.ExtractedText);

        // 5. Download Document
        var downloadResponse = await client.GetAsync($"/api/workspaces/{wsId}/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileContent, Encoding.UTF8.GetString(downloadedBytes));

        // 6. Delete Document
        var deleteResponse = await client.DeleteAsync($"/api/workspaces/{wsId}/documents/{doc.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // 7. Verify Document is no longer returned in list
        var listAfterDelete = await client.GetAsync($"/api/workspaces/{wsId}/documents");
        var emptyList = await listAfterDelete.Content.ReadFromJsonAsync<List<DocumentSummaryDto>>();
        Assert.Empty(emptyList!);
    }

    [Fact]
    public async Task Upload_Unsupported_Format_Should_Return_400()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("doc_invalid");
        var wsId = workspace.Id;

        using var formData = new MultipartFormDataContent();
        var fileStreamContent = new ByteArrayContent(Encoding.UTF8.GetBytes("echo hello"));
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-sh");
        formData.Add(fileStreamContent, "file", "script.sh");

        var response = await client.PostAsync($"/api/workspaces/{wsId}/documents", formData);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Workspace_Isolation_Other_User_Cannot_Access_Document()
    {
        var (clientA, userA, workspaceA) = await CreateUserAndWorkspaceAsync("user_a");
        var (clientB, userB, workspaceB) = await CreateUserAndWorkspaceAsync("user_b");

        // Upload doc in workspace A
        using var formData = new MultipartFormDataContent();
        var fileStreamContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Secret doc in A"));
        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formData.Add(fileStreamContent, "file", "secret.txt");

        var uploadResponse = await clientA.PostAsync($"/api/workspaces/{workspaceA.Id}/documents", formData);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var docA = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        // User B attempts to access Document A
        var unauthorizedGet = await clientB.GetAsync($"/api/workspaces/{workspaceA.Id}/documents/{docA!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedGet.StatusCode);

        // User B attempts to delete Document A
        var unauthorizedDelete = await clientB.DeleteAsync($"/api/workspaces/{workspaceA.Id}/documents/{docA.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedDelete.StatusCode);
    }
}
