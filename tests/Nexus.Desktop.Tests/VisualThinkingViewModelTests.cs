using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Desktop.Models;
using Nexus.Desktop.Tests.Fakes;
using Nexus.Desktop.ViewModels;
using Nexus.Domain.Enums;
using Xunit;

namespace Nexus.Desktop.Tests;

public class VisualThinkingViewModelTests
{
    private readonly FakeApiClient _fakeApiClient;
    private readonly FakeDialogService _fakeDialogService;
    private readonly FakeNavigationService _fakeNavigationService;
    private readonly UserSession _userSession;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public VisualThinkingViewModelTests()
    {
        _fakeApiClient = new FakeApiClient();
        _fakeDialogService = new FakeDialogService();
        _fakeNavigationService = new FakeNavigationService();
        _userSession = new UserSession
        {
            CurrentUser = new AuthResponse(Guid.NewGuid(), "test@nexus.ai", "Test User", "User", "fake.token", DateTime.UtcNow.AddDays(1)),
            SelectedWorkspace = new WorkspaceDto(_workspaceId, "Main Workspace", "Description", "🚀", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)
        };
    }

    #region BoardsViewModel Tests

    [Fact]
    public async Task BoardsViewModel_LoadBoardsAsync_PopulatesBoards()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Sprint 42", null, BoardType.Kanban, 1, DateTime.UtcNow, null));
        _fakeApiClient.BoardItems.Add(new BoardItemDto(Guid.NewGuid(), boardId, null, BoardItemType.StickyNote, "Note 1", null, "Text", 50, 50, 150, 100, 0, 1, "#FFF", null, null, DateTime.UtcNow, null));

        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();

        Assert.Single(vm.Boards);
        Assert.Equal("Sprint 42", vm.Boards[0].Title);
        Assert.NotNull(vm.SelectedBoard);
        Assert.Single(vm.CanvasItems);
        Assert.Equal("Note 1", vm.CanvasItems[0].Title);
    }

    [Fact]
    public async Task BoardsViewModel_CreateBoardAsync_AddsNewBoard()
    {
        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        vm.NewBoardTitle = "Roadmap 2027";

        await vm.CreateBoardAsync();

        Assert.Single(vm.Boards);
        Assert.Equal("Roadmap 2027", vm.Boards[0].Title);
        Assert.NotNull(vm.SelectedBoard);
        Assert.Equal("Roadmap 2027", vm.SelectedBoard.Title);
    }

    [Fact]
    public async Task BoardsViewModel_AddBoardItem_CreatesItem_AndSupportsUndoRedo()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Canvas Board", null, BoardType.Kanban, 0, DateTime.UtcNow, null));

        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();

        // Add Item
        await vm.AddItemAsync("StickyNote");
        Assert.Single(vm.CanvasItems);
        var item = vm.CanvasItems[0];
        var originalX = item.X;
        var originalY = item.Y;

        // Move Item & verify Undo/Redo
        vm.MoveItem(item.Id, originalX + 100, originalY + 100);
        Assert.True(vm.UndoRedo.CanUndo);
        Assert.Equal(originalX + 100, item.X);

        // Undo
        vm.UndoCommand.Execute(null);
        Assert.Equal(originalX, item.X);
        Assert.True(vm.UndoRedo.CanRedo);

        // Redo
        vm.RedoCommand.Execute(null);
        Assert.Equal(originalX + 100, item.X);
    }

    [Fact]
    public async Task BoardsViewModel_ZoomAndPan_ClampsAndResets()
    {
        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);

        vm.ZoomIn();
        Assert.Equal(1.1, Math.Round(vm.ZoomLevel, 1));

        vm.ZoomOut();
        Assert.Equal(1.0, Math.Round(vm.ZoomLevel, 1));

        // Test Clamp Lower Bound
        vm.ZoomLevel = 0.05;
        vm.ZoomOut();
        Assert.True(vm.ZoomLevel >= 0.1);

        // Test Clamp Upper Bound
        vm.ZoomLevel = 4.5;
        vm.ZoomIn();
        Assert.True(vm.ZoomLevel <= 4.0);

        // Test Reset
        vm.PanX = 150;
        vm.PanY = -200;
        vm.ResetZoom();
        Assert.Equal(1.0, vm.ZoomLevel);
        Assert.Equal(0, vm.PanX);
        Assert.Equal(0, vm.PanY);
    }

    [Fact]
    public async Task BoardsViewModel_DeleteCurrentBoard_RemovesBoard()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Board To Delete", null, BoardType.Kanban, 0, DateTime.UtcNow, null));

        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();
        Assert.Single(vm.Boards);

        await vm.DeleteCurrentBoardAsync();

        Assert.Empty(vm.Boards);
        Assert.Null(vm.SelectedBoard);
        Assert.Empty(vm.CanvasItems);
    }

    #endregion

    #region MindMapsViewModel Tests

    [Fact]
    public async Task MindMapsViewModel_LoadMindMapsAsync_PopulatesMapsAndNodes()
    {
        var mapId = Guid.NewGuid();
        var node1Id = Guid.NewGuid();
        var node2Id = Guid.NewGuid();
        var edgeId = Guid.NewGuid();

        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Domain Model", null, node1Id, 2, 1, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(node1Id, mapId, null, "Root Entity", null, 100, 100, 180, 80, "#8B5CF6", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(node2Id, mapId, node1Id, "Value Object", null, 350, 100, 180, 80, "#3B82F6", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null));
        _fakeApiClient.MindMapEdges.Add(new MindMapEdgeDto(edgeId, mapId, node1Id, node2Id, "contains", null, "Solid", MindMapEdgeType.Contains, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        Assert.Single(vm.MindMaps);
        Assert.Equal("Domain Model", vm.MindMaps[0].Title);
        Assert.NotNull(vm.SelectedMindMap);
        Assert.Equal(2, vm.CanvasNodes.Count);
        Assert.Single(vm.CanvasEdges);
        Assert.Equal(node1Id, vm.CanvasEdges[0].SourceNodeId);
        Assert.Equal(node2Id, vm.CanvasEdges[0].TargetNodeId);
    }

    [Fact]
    public async Task MindMapsViewModel_AddNode_CreatesNode_AndSupportsUndo()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Concept Map", null, null, 0, 0, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Event Store");
        Assert.Single(vm.CanvasNodes);
        Assert.Equal("Event Store", vm.CanvasNodes[0].Title);

        var node = vm.CanvasNodes[0];
        var originalX = node.X;
        var originalY = node.Y;

        // Move node & verify Undo/Redo
        vm.MoveNode(node.Id, originalX + 150, originalY + 150);
        Assert.True(vm.UndoRedo.CanUndo);
        Assert.Equal(originalX + 150, node.X);

        // Undo
        vm.UndoCommand.Execute(null);
        Assert.Equal(originalX, node.X);
        Assert.True(vm.UndoRedo.CanRedo);

        // Redo
        vm.RedoCommand.Execute(null);
        Assert.Equal(originalX + 150, node.X);
    }

    [Fact]
    public async Task MindMapsViewModel_ConnectNodes_CreatesEdge()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Graph Map", null, null, 0, 0, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Node A");
        vm.SelectedNode = null; // Deselect so Node B doesn't auto-connect to Node A
        await vm.AddNodeAsync("Node B");

        var nodeA = vm.CanvasNodes[0];
        var nodeB = vm.CanvasNodes[1];

        await vm.ConnectNodesAsync(nodeA.Id, nodeB.Id);
        Assert.Single(vm.CanvasEdges);
        Assert.Equal(nodeA.Id, vm.CanvasEdges[0].SourceNodeId);
        Assert.Equal(nodeB.Id, vm.CanvasEdges[0].TargetNodeId);
    }

    [Fact]
    public async Task MindMapsViewModel_ApplyAutoLayout_UpdatesPositions()
    {
        var mapId = Guid.NewGuid();
        var n1 = Guid.NewGuid();
        var n2 = Guid.NewGuid();

        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Layout Map", null, n1, 2, 1, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(n1, mapId, null, "Root", null, 0, 0, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(n2, mapId, n1, "Child", null, 0, 0, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null));
        _fakeApiClient.MindMapEdges.Add(new MindMapEdgeDto(Guid.NewGuid(), mapId, n1, n2, null, null, "Solid", MindMapEdgeType.RelatesTo, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.ApplyAutoLayoutAsync("HorizontalTree");

        // Auto-layout should reposition nodes
        Assert.NotNull(vm.CanvasNodes);
        Assert.Equal(2, vm.CanvasNodes.Count);
    }

    [Fact]
    public async Task MindMapsViewModel_ExplainNode_OpensAiPanel()
    {
        var mapId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Explain Map", null, nodeId, 1, 0, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(nodeId, mapId, null, "CQRS Architecture", null, 100, 100, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        vm.SelectedNode = vm.CanvasNodes[0];

        await vm.ExplainSelectedNodeAsync();

        Assert.True(vm.IsAiPanelOpen);
        Assert.False(string.IsNullOrWhiteSpace(vm.AiExplanationText));
        Assert.NotEmpty(vm.AiKeyTakeaways);
    }

    [Fact]
    public async Task MindMapsViewModel_FindRelatedKnowledge_PopulatesDrawer()
    {
        var mapId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Related Map", null, nodeId, 1, 0, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(nodeId, mapId, null, "Event Store", null, 100, 100, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        vm.SelectedNode = vm.CanvasNodes[0];

        await vm.FindRelatedKnowledgeAsync();

        Assert.True(vm.IsAiPanelOpen);
        Assert.NotNull(vm.RelatedKnowledgeItems);
    }

    [Fact]
    public async Task MindMapsViewModel_NavigateToKnowledge_InvokesNavigationService()
    {
        var mapId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var pageId = Guid.NewGuid();

        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Nav Map", null, nodeId, 1, 0, DateTime.UtcNow, null));
        _fakeApiClient.MindMapNodes.Add(new MindMapNodeDto(nodeId, mapId, null, "Knowledge Node", null, 100, 100, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, "Page", pageId, DateTime.UtcNow, null));

        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        vm.SelectedNode = vm.CanvasNodes[0];

        vm.NavigateToKnowledge(vm.SelectedNode);

        Assert.Equal(1, _fakeNavigationService.NavigationCount);
        Assert.Equal(typeof(PagesViewModel), _fakeNavigationService.LastNavigatedType);
    }

    #endregion
}
