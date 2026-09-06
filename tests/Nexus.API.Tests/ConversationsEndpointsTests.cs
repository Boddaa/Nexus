using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Workspaces;
using Xunit;

namespace Nexus.API.Tests;

public class ConversationsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConversationsEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task ListConversations_Without_Authentication_Should_Return_401()
    {
        var response = await _client.GetAsync($"/api/workspaces/{Guid.NewGuid()}/conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateConversation_Valid_Should_Return_200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("conv_create");
        var req = new CreateConversationRequest("Research Chat");

        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/conversations", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var conv = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(conv);
        Assert.Equal("Research Chat", conv.Title);
        Assert.Equal(workspace.Id, conv.WorkspaceId);
    }

    [Fact]
    public async Task SendMessage_EmptyContent_Should_Return_400()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("conv_empty_msg");
        var createRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/conversations", new CreateConversationRequest("Chat"));
        var conv = (await createRes.Content.ReadFromJsonAsync<ConversationDto>())!;

        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/conversations/{conv.Id}/messages", new SendChatMessageRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Conversation_Authorization_UserA_OwnConversation_Allowed_Returns_200()
    {
        var (clientA, _, workspaceA) = await CreateUserAndWorkspaceAsync("user_a_own");
        var createRes = await clientA.PostAsJsonAsync($"/api/workspaces/{workspaceA.Id}/conversations", new CreateConversationRequest("Alice Workspace Chat"));
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var convA = (await createRes.Content.ReadFromJsonAsync<ConversationDto>())!;

        // User A views own conversation -> 200 OK
        var response = await clientA.GetAsync($"/api/workspaces/{workspaceA.Id}/conversations/{convA.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(fetched);
        Assert.Equal(convA.Id, fetched.Id);
        Assert.Equal("Alice Workspace Chat", fetched.Title);
    }

    [Fact]
    public async Task Conversation_Authorization_UserB_UserAConversation_Denied_Returns_NotFoundOrUnauthorized()
    {
        var (clientA, _, workspaceA) = await CreateUserAndWorkspaceAsync("user_a_priv");
        var (clientB, _, _) = await CreateUserAndWorkspaceAsync("user_b_priv");

        var createRes = await clientA.PostAsJsonAsync($"/api/workspaces/{workspaceA.Id}/conversations", new CreateConversationRequest("Private Chat"));
        var convA = (await createRes.Content.ReadFromJsonAsync<ConversationDto>())!;

        // User B tries to view User A's conversation -> Denied (401 or 404)
        var response = await clientB.GetAsync($"/api/workspaces/{workspaceA.Id}/conversations/{convA.Id}");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Conversation_Authorization_UserA_ConversationInAnotherWorkspace_Denied_Returns_NotFoundOrUnauthorized()
    {
        var (clientA, _, workspaceA) = await CreateUserAndWorkspaceAsync("user_a_ws1");
        var createRes = await clientA.PostAsJsonAsync($"/api/workspaces/{workspaceA.Id}/conversations", new CreateConversationRequest("WsA Chat"));
        var convA = (await createRes.Content.ReadFromJsonAsync<ConversationDto>())!;

        // User A tries to view the conversation specifying a different workspace ID -> Denied (404 or 401)
        var randomWorkspaceId = Guid.NewGuid();
        var response = await clientA.GetAsync($"/api/workspaces/{randomWorkspaceId}/conversations/{convA.Id}");
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Unauthorized);
    }
}
