using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Enums;
using Xunit;

namespace Nexus.API.Tests;

public class VisualThinkingEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public VisualThinkingEndpointsTests(CustomWebApplicationFactory factory)
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

    #region Boards API Tests

    [Fact]
    public async Task Boards_CRUD_And_Items_Flow_Should_Work_EndToEnd()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("board_crud");
        var wsId = workspace.Id;

        // 1. Create Board
        var createBoardReq = new CreateBoardRequest("Sprint Planning", "Quarterly roadmap visualizer", BoardType.Kanban);
        var createBoardRes = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/boards", createBoardReq);
        Assert.Equal(HttpStatusCode.Created, createBoardRes.StatusCode);
        var board = await createBoardRes.Content.ReadFromJsonAsync<BoardDto>();
        Assert.NotNull(board);
        Assert.Equal("Sprint Planning", board.Title);

        // 2. List Boards
        var listRes = await client.GetAsync($"/api/workspaces/{wsId}/boards");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var boards = await listRes.Content.ReadFromJsonAsync<List<BoardDto>>();
        Assert.NotNull(boards);
        Assert.Contains(boards, b => b.Id == board.Id);

        // 3. Get Board Detail
        var detailRes = await client.GetAsync($"/api/workspaces/{wsId}/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = await detailRes.Content.ReadFromJsonAsync<BoardDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("Sprint Planning", detail.Title);
        Assert.Empty(detail.Items);

        // 4. Create Board Items
        var item1Req = new CreateBoardItemRequest(BoardItemType.StickyNote, "Feature A", X: 50, Y: 100, Width: 150, Height: 120, ColorHex: "#FFFF00");
        var item1Res = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/boards/{board.Id}/items", item1Req);
        Assert.Equal(HttpStatusCode.Created, item1Res.StatusCode);
        var item1 = await item1Res.Content.ReadFromJsonAsync<BoardItemDto>();
        Assert.NotNull(item1);
        Assert.Equal("Feature A", item1.Title);

        var item2Req = new CreateBoardItemRequest(BoardItemType.Shape, "Divider", X: 300, Y: 100, Width: 200, Height: 100, ColorHex: "#3B82F6");
        var item2Res = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/boards/{board.Id}/items", item2Req);
        Assert.Equal(HttpStatusCode.Created, item2Res.StatusCode);
        var item2 = await item2Res.Content.ReadFromJsonAsync<BoardItemDto>();
        Assert.NotNull(item2);

        // 5. Batch Update Board Items
        var batchReq = new BatchUpdateBoardItemsRequest(new List<BoardItemBatchPositionDto>
        {
            new(item1.Id, 120, 180, 160, 130, 15, 2),
            new(item2.Id, 450, 200, 220, 110, 0, 1)
        });
        var batchRes = await client.PatchAsJsonAsync($"/api/workspaces/{wsId}/boards/{board.Id}/items/batch", batchReq);
        Assert.Equal(HttpStatusCode.OK, batchRes.StatusCode);
        var updatedItems = await batchRes.Content.ReadFromJsonAsync<List<BoardItemDto>>();
        Assert.NotNull(updatedItems);
        Assert.Equal(2, updatedItems.Count);

        // 6. Delete One Item
        var deleteItemRes = await client.DeleteAsync($"/api/workspaces/{wsId}/boards/{board.Id}/items/{item1.Id}");
        Assert.True(deleteItemRes.IsSuccessStatusCode);

        // 7. Verify items count is now 1
        var itemsAfterDelete = await client.GetFromJsonAsync<List<BoardItemDto>>($"/api/workspaces/{wsId}/boards/{board.Id}/items");
        Assert.NotNull(itemsAfterDelete);
        Assert.Single(itemsAfterDelete);
        Assert.Equal(item2.Id, itemsAfterDelete[0].Id);

        // 8. Delete Board
        var deleteBoardRes = await client.DeleteAsync($"/api/workspaces/{wsId}/boards/{board.Id}");
        Assert.True(deleteBoardRes.IsSuccessStatusCode);

        // 9. Verify Board is gone
        var getGoneRes = await client.GetAsync($"/api/workspaces/{wsId}/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getGoneRes.StatusCode);
    }

    #endregion

    #region Mind Maps API Tests

    [Fact]
    public async Task MindMaps_CRUD_And_Graph_Flow_Should_Work_EndToEnd()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("mindmap_crud");
        var wsId = workspace.Id;

        // 1. Create Mind Map
        var createMapReq = new CreateMindMapRequest("System Architecture", "High level components");
        var createMapRes = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/mindmaps", createMapReq);
        Assert.Equal(HttpStatusCode.Created, createMapRes.StatusCode);
        var mindMap = await createMapRes.Content.ReadFromJsonAsync<MindMapDto>();
        Assert.NotNull(mindMap);
        Assert.Equal("System Architecture", mindMap.Title);

        // 2. Add Nodes
        var node1Req = new CreateMindMapNodeRequest("Gateway API", X: 100, Y: 100);
        var node1Res = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/nodes", node1Req);
        Assert.True(node1Res.IsSuccessStatusCode);
        var node1 = await node1Res.Content.ReadFromJsonAsync<MindMapNodeDto>();
        Assert.NotNull(node1);

        var node2Req = new CreateMindMapNodeRequest("Auth Service", X: 400, Y: 100);
        var node2Res = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/nodes", node2Req);
        Assert.True(node2Res.IsSuccessStatusCode);
        var node2 = await node2Res.Content.ReadFromJsonAsync<MindMapNodeDto>();
        Assert.NotNull(node2);

        // 3. Connect with Edge
        var edgeReq = new CreateMindMapEdgeRequest(node1.Id, node2.Id, Label: "authenticates");
        var edgeRes = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/edges", edgeReq);
        Assert.True(edgeRes.IsSuccessStatusCode);
        var edge = await edgeRes.Content.ReadFromJsonAsync<MindMapEdgeDto>();
        Assert.NotNull(edge);
        Assert.Equal("authenticates", edge.Label);

        // 4. Batch update node positions
        var batchReq = new BatchUpdateMindMapNodesRequest(new List<MindMapNodeBatchPositionDto>
        {
            new(node1.Id, 150, 150, 200, 90),
            new(node2.Id, 450, 150, 200, 90)
        });
        var batchRes = await client.PatchAsJsonAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/nodes/batch", batchReq);
        Assert.Equal(HttpStatusCode.OK, batchRes.StatusCode);

        // 5. Test Auto-Layout endpoint
        var layoutReq = new ApplyLayoutRequest(AutoLayoutAlgorithm.HorizontalTree);
        var layoutRes = await client.PostAsJsonAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/layout?persist=false", layoutReq);
        Assert.Equal(HttpStatusCode.OK, layoutRes.StatusCode);
        var layout = await layoutRes.Content.ReadFromJsonAsync<LayoutResultDto>();
        Assert.NotNull(layout);
        Assert.Equal(2, layout.Positions.Count);

        // 6. Delete edge
        var deleteEdgeRes = await client.DeleteAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/edges/{edge.Id}");
        Assert.True(deleteEdgeRes.IsSuccessStatusCode);

        // 7. Delete node
        var deleteNodeRes = await client.DeleteAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}/nodes/{node2.Id}");
        Assert.True(deleteNodeRes.IsSuccessStatusCode);

        // 8. Delete mind map
        var deleteMapRes = await client.DeleteAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}");
        Assert.True(deleteMapRes.IsSuccessStatusCode);

        // 9. Verify mind map is gone
        var getGoneRes = await client.GetAsync($"/api/workspaces/{wsId}/mindmaps/{mindMap.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getGoneRes.StatusCode);
    }

    #endregion

    #region Security & Workspace Isolation Tests

    [Fact]
    public async Task Cross_Workspace_Access_Should_Return_Forbidden_Or_NotFound()
    {
        var (clientUser1, _, workspace1) = await CreateUserAndWorkspaceAsync("user1_iso");
        var (clientUser2, _, workspace2) = await CreateUserAndWorkspaceAsync("user2_iso");

        // User 1 creates board in Workspace 1
        var boardReq = new CreateBoardRequest("Confidential Board", null, BoardType.Kanban);
        var boardRes = await clientUser1.PostAsJsonAsync($"/api/workspaces/{workspace1.Id}/boards", boardReq);
        boardRes.EnsureSuccessStatusCode();
        var board = (await boardRes.Content.ReadFromJsonAsync<BoardDto>())!;

        // User 2 attempts to fetch User 1's board in Workspace 1
        var unauthorizedRes = await clientUser2.GetAsync($"/api/workspaces/{workspace1.Id}/boards/{board.Id}");
        Assert.True(
            unauthorizedRes.StatusCode == HttpStatusCode.Unauthorized ||
            unauthorizedRes.StatusCode == HttpStatusCode.Forbidden ||
            unauthorizedRes.StatusCode == HttpStatusCode.NotFound);

        // User 2 attempts to fetch User 1's board using Workspace 2 ID
        var mismatchRes = await clientUser2.GetAsync($"/api/workspaces/{workspace2.Id}/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NotFound, mismatchRes.StatusCode);
    }

    #endregion
}
