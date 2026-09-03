using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Xunit;

namespace Nexus.API.Tests;

public class PagesAndNotesEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PagesAndNotesEndpointsTests(CustomWebApplicationFactory factory)
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

    #region Pages API Tests

    [Fact]
    public async Task Pages_CRUD_And_Hierarchy_Flow_Should_Work_EndToEnd()
    {
        // 1. Authenticate user
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("page_crud");
        var wsId = workspace.Id;

        // 2. Create Root Page
        var createRootRequest = new CreatePageRequest("Architecture Root", "🏛️", null, "{\"type\":\"root\"}", null, 0);
        var createRootResponse = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", createRootRequest);

        Assert.Equal(HttpStatusCode.Created, createRootResponse.StatusCode);
        var rootPage = await createRootResponse.Content.ReadFromJsonAsync<PageDto>();
        Assert.NotNull(rootPage);
        Assert.Equal("Architecture Root", rootPage.Title);
        Assert.Null(rootPage.ParentPageId);

        // 3. Create Child Page
        var createChildRequest = new CreatePageRequest("Domain Layer Subpage", "📦", null, "{}", rootPage.Id, 1);
        var createChildResponse = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", createChildRequest);

        Assert.Equal(HttpStatusCode.Created, createChildResponse.StatusCode);
        var childPage = await createChildResponse.Content.ReadFromJsonAsync<PageDto>();
        Assert.NotNull(childPage);
        Assert.Equal(rootPage.Id, childPage.ParentPageId);

        // 4. Get Page Tree
        var treeResponse = await client.GetAsync($"/api/workspaces/{wsId}/pages");
        Assert.Equal(HttpStatusCode.OK, treeResponse.StatusCode);
        var tree = await treeResponse.Content.ReadFromJsonAsync<List<PageTreeNodeDto>>();
        Assert.NotNull(tree);
        Assert.Contains(tree, p => p.Id == rootPage.Id);

        var rootInTree = tree.First(p => p.Id == rootPage.Id);
        Assert.Single(rootInTree.Children);
        Assert.Equal(childPage.Id, rootInTree.Children[0].Id);

        // 5. Get Page By ID
        var getByIdResponse = await client.GetAsync($"/api/workspaces/{wsId}/pages/{childPage.Id}");
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        var fetchedPage = await getByIdResponse.Content.ReadFromJsonAsync<PageDto>();
        Assert.NotNull(fetchedPage);
        Assert.Equal("Domain Layer Subpage", fetchedPage.Title);

        // 6. Update Page
        var updateRequest = new UpdatePageRequest("Domain Layer (Updated)", "✨", null, "{\"updated\":true}", 2);
        var updateResponse = await client.PutAsJsonAsync($"/api/workspaces/{wsId}/pages/{childPage.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedPage = await updateResponse.Content.ReadFromJsonAsync<PageDto>();
        Assert.NotNull(updatedPage);
        Assert.Equal("Domain Layer (Updated)", updatedPage.Title);

        // 7. Move Page (Make Child a Root page)
        var moveRequest = new MovePageRequest(null, 5);
        var moveResponse = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages/{childPage.Id}/move", moveRequest);
        Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);

        // 8. Delete Page (Soft Delete)
        var deleteResponse = await client.DeleteAsync($"/api/workspaces/{wsId}/pages/{rootPage.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify root page is gone from active tree
        var treeAfterDelete = await (await client.GetAsync($"/api/workspaces/{wsId}/pages")).Content.ReadFromJsonAsync<List<PageTreeNodeDto>>();
        Assert.DoesNotContain(treeAfterDelete!, p => p.Id == rootPage.Id);
    }

    [Fact]
    public async Task MovePage_Cyclic_Dependency_Should_Return_409_Conflict()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("page_cycle");
        var wsId = workspace.Id;

        // Root -> Child -> SubChild
        var root = (await (await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", new CreatePageRequest("Page A"))).Content.ReadFromJsonAsync<PageDto>())!;
        var child = (await (await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", new CreatePageRequest("Page B", "📄", null, "{}", root.Id, 0))).Content.ReadFromJsonAsync<PageDto>())!;
        var subChild = (await (await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", new CreatePageRequest("Page C", "📄", null, "{}", child.Id, 0))).Content.ReadFromJsonAsync<PageDto>())!;

        // Attempt to move Root under SubChild
        var moveResponse = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages/{root.Id}/move", new MovePageRequest(subChild.Id, 0));

        Assert.Equal(HttpStatusCode.Conflict, moveResponse.StatusCode);
    }

    [Fact]
    public async Task Pages_Invalid_Validation_Should_Return_400_BadRequest()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("page_val");
        var wsId = workspace.Id;

        var invalidRequest = new CreatePageRequest("", "IconTooLong12345", null, "{}", null, -1);
        var response = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pages_Unauthorized_Should_Return_401()
    {
        var unauthenticatedClient = _client;
        var response = await unauthenticatedClient.GetAsync($"/api/workspaces/{Guid.NewGuid()}/pages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pages_CrossWorkspace_Access_Should_Be_Rejected()
    {
        var (clientA, userA, workspaceA) = await CreateUserAndWorkspaceAsync("user_a");
        var (clientB, userB, workspaceB) = await CreateUserAndWorkspaceAsync("user_b");

        // User A tries to get User B's pages
        var response = await clientA.GetAsync($"/api/workspaces/{workspaceB.Id}/pages");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Notes API Tests

    [Fact]
    public async Task Notes_CRUD_And_Filtering_Flow_Should_Work_EndToEnd()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("note_crud");
        var wsId = workspace.Id;

        // 1. Create a Page first to associate with note
        var page = (await (await client.PostAsJsonAsync($"/api/workspaces/{wsId}/pages", new CreatePageRequest("Associated Page"))).Content.ReadFromJsonAsync<PageDto>())!;

        // 2. Create Note with Page and Tags
        var createNoteRequest = new CreateNoteRequest(
            "Design Patterns in C#",
            "# Specification\n- Use Factory and Strategy patterns",
            "markdown",
            true,
            page.Id,
            new List<string> { "Architecture", "Design" });

        var createResponse = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/notes", createNoteRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var note = await createResponse.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(note);
        Assert.Equal("Design Patterns in C#", note.Title);
        Assert.True(note.IsPinned);
        Assert.Equal(page.Id, note.PageId);
        Assert.Contains("Architecture", note.Tags);

        // 3. Create Standalone Note
        var standaloneNote = (await (await client.PostAsJsonAsync($"/api/workspaces/{wsId}/notes", new CreateNoteRequest("Standalone Note", "Content", "markdown", false))).Content.ReadFromJsonAsync<NoteDto>())!;

        // 4. Get Notes List
        var listResponse = await client.GetAsync($"/api/workspaces/{wsId}/notes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var allNotes = await listResponse.Content.ReadFromJsonAsync<List<NoteSummaryDto>>();
        Assert.NotNull(allNotes);
        Assert.Equal(2, allNotes.Count);

        // 5. Filter by PageId
        var pageFilteredResponse = await client.GetAsync($"/api/workspaces/{wsId}/notes?pageId={page.Id}");
        Assert.Equal(HttpStatusCode.OK, pageFilteredResponse.StatusCode);
        var pageNotes = await pageFilteredResponse.Content.ReadFromJsonAsync<List<NoteSummaryDto>>();
        Assert.Single(pageNotes!);
        Assert.Equal(note.Id, pageNotes![0].Id);

        // 6. Filter by IsPinned
        var pinnedResponse = await client.GetAsync($"/api/workspaces/{wsId}/notes?isPinned=true");
        Assert.Equal(HttpStatusCode.OK, pinnedResponse.StatusCode);
        var pinnedNotes = await pinnedResponse.Content.ReadFromJsonAsync<List<NoteSummaryDto>>();
        Assert.Single(pinnedNotes!);
        Assert.True(pinnedNotes![0].IsPinned);

        // 7. Get Note By ID
        var getByIdResponse = await client.GetAsync($"/api/workspaces/{wsId}/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        var fetchedNote = await getByIdResponse.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(fetchedNote);
        Assert.Equal("Design Patterns in C#", fetchedNote.Title);

        // 8. Update Note
        var updateRequest = new UpdateNoteRequest("Design Patterns (Updated)", "New Content", "markdown", false, null, new List<string> { "Updated" });
        var updateResponse = await client.PutAsJsonAsync($"/api/workspaces/{wsId}/notes/{note.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedNote = await updateResponse.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(updatedNote);
        Assert.Equal("Design Patterns (Updated)", updatedNote.Title);
        Assert.False(updatedNote.IsPinned);

        // 9. Delete Note
        var deleteResponse = await client.DeleteAsync($"/api/workspaces/{wsId}/notes/{standaloneNote.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var remainingNotes = await (await client.GetAsync($"/api/workspaces/{wsId}/notes")).Content.ReadFromJsonAsync<List<NoteSummaryDto>>();
        Assert.DoesNotContain(remainingNotes!, n => n.Id == standaloneNote.Id);
    }

    [Fact]
    public async Task Notes_Validation_Should_Return_400_BadRequest()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("note_val");
        var wsId = workspace.Id;

        var invalidRequest = new CreateNoteRequest("", "content", "invalid_content_type");
        var response = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/notes", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Notes_CrossWorkspace_Access_Should_Be_Rejected()
    {
        var (clientA, userA, workspaceA) = await CreateUserAndWorkspaceAsync("user_na");
        var (clientB, userB, workspaceB) = await CreateUserAndWorkspaceAsync("user_nb");

        var response = await clientA.GetAsync($"/api/workspaces/{workspaceB.Id}/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
