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

    [Fact]
    public async Task BoardsViewModel_MultiSelection_SelectAll_And_ClearSelection()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Selection Board", null, BoardType.Kanban, 0, DateTime.UtcNow, null));
        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();

        await vm.AddItemAsync("StickyNote");
        await vm.AddItemAsync("StickyNote");
        await vm.AddItemAsync("StickyNote");
        Assert.Equal(3, vm.CanvasItems.Count);

        // 1. Select All
        vm.SelectAllCommand.Execute(null);
        Assert.Equal(3, vm.SelectedItems.Count);
        Assert.All(vm.CanvasItems, item => Assert.True(item.IsSelected));

        // 2. Clear Selection
        vm.ClearSelectionCommand.Execute(null);
        Assert.Empty(vm.SelectedItems);
        Assert.All(vm.CanvasItems, item => Assert.False(item.IsSelected));

        // 3. Toggle Selection (Ctrl+click)
        var firstItem = vm.CanvasItems[0];
        vm.ToggleItemSelection(firstItem);
        Assert.Single(vm.SelectedItems);
        Assert.True(firstItem.IsSelected);

        vm.ToggleItemSelection(firstItem);
        Assert.Empty(vm.SelectedItems);
        Assert.False(firstItem.IsSelected);

        // 4. Set Single Selection
        var secondItem = vm.CanvasItems[1];
        vm.SetSingleSelection(secondItem);
        Assert.Single(vm.SelectedItems);
        Assert.Equal(secondItem, vm.SelectedItems[0]);
    }

    [Fact]
    public async Task BoardsViewModel_MultiItemDrag_MovesAllSelectedItems_And_SupportsUndo()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Drag Board", null, BoardType.Kanban, 0, DateTime.UtcNow, null));
        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();

        await vm.AddItemAsync("StickyNote");
        await vm.AddItemAsync("StickyNote");
        var item1 = vm.CanvasItems[0];
        var item2 = vm.CanvasItems[1];

        var origX1 = item1.X;
        var origY1 = item1.Y;
        var origX2 = item2.X;
        var origY2 = item2.Y;

        // Select both items
        vm.SelectAllCommand.Execute(null);

        // Drag both items together
        vm.MoveSelectedItems(60, 80);
        Assert.Equal(origX1 + 60, item1.X);
        Assert.Equal(origY1 + 80, item1.Y);
        Assert.Equal(origX2 + 60, item2.X);
        Assert.Equal(origY2 + 80, item2.Y);

        // Undo
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(origX1, item1.X);
        Assert.Equal(origY1, item1.Y);
        Assert.Equal(origX2, item2.X);
        Assert.Equal(origY2, item2.Y);

        // Redo
        Assert.True(vm.UndoRedo.CanRedo);
        vm.RedoCommand.Execute(null);
        Assert.Equal(origX1 + 60, item1.X);
        Assert.Equal(origY1 + 80, item1.Y);
        Assert.Equal(origX2 + 60, item2.X);
        Assert.Equal(origY2 + 80, item2.Y);
    }

    [Fact]
    public async Task BoardsViewModel_ResizeItem_ClampsToBounds_And_SupportsUndo()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Resize Board", null, BoardType.Kanban, 0, DateTime.UtcNow, null));
        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();

        await vm.AddItemAsync("StickyNote");
        var item = vm.CanvasItems[0];
        var originalW = item.Width;
        var originalH = item.Height;

        // Resize below minimum bounds (min 50 width, min 40 height)
        vm.ResizeItem(item.Id, 30, 20);
        Assert.Equal(50, item.Width);
        Assert.Equal(40, item.Height);

        // Undo restores original size
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(originalW, item.Width);
        Assert.Equal(originalH, item.Height);

        // Redo restores clamped size
        vm.RedoCommand.Execute(null);
        Assert.Equal(50, item.Width);
        Assert.Equal(40, item.Height);
    }

    [Fact]
    public async Task BoardsViewModel_CopyPaste_And_DeleteSelected_SupportsUndo()
    {
        var boardId = Guid.NewGuid();
        _fakeApiClient.Boards.Add(new BoardDto(boardId, _workspaceId, null, "Clipboard Board", null, BoardType.Kanban, 0, DateTime.UtcNow, null));
        var vm = new BoardsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadBoardsAsync();

        await vm.AddItemAsync("StickyNote");
        var item = vm.CanvasItems[0];
        vm.SetSingleSelection(item);

        // Copy and Paste
        vm.CopySelectedCommand.Execute(null);
        await vm.PasteAsync();

        Assert.Equal(2, vm.CanvasItems.Count);
        var pastedItem = vm.CanvasItems.First(i => i.Id != item.Id);
        Assert.Equal(200, pastedItem.X);
        Assert.Equal(150, pastedItem.Y);

        // Select all and Delete
        vm.SelectAllCommand.Execute(null);
        await vm.DeleteSelectedAsync();
        Assert.Empty(vm.CanvasItems);

        // Undo restore deleted items
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(2, vm.CanvasItems.Count);
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

    [Fact]
    public async Task MindMapsViewModel_MultiSelection_SelectAll_And_ClearSelection()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Selection Map", null, null, 0, 0, DateTime.UtcNow, null));
        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Node 1");
        vm.SelectedNode = null;
        await vm.AddNodeAsync("Node 2");
        vm.SelectedNode = null;
        await vm.AddNodeAsync("Node 3");
        Assert.Equal(3, vm.CanvasNodes.Count);

        // 1. Select All
        vm.SelectAllCommand.Execute(null);
        Assert.Equal(3, vm.SelectedNodes.Count);
        Assert.All(vm.CanvasNodes, n => Assert.True(n.IsSelected));

        // 2. Clear Selection
        vm.ClearSelectionCommand.Execute(null);
        Assert.Empty(vm.SelectedNodes);
        Assert.All(vm.CanvasNodes, n => Assert.False(n.IsSelected));

        // 3. Toggle Selection (Ctrl+click)
        var n1 = vm.CanvasNodes[0];
        vm.ToggleNodeSelection(n1);
        Assert.Single(vm.SelectedNodes);
        Assert.True(n1.IsSelected);

        vm.ToggleNodeSelection(n1);
        Assert.Empty(vm.SelectedNodes);
        Assert.False(n1.IsSelected);

        // 4. Set Single Selection
        var n2 = vm.CanvasNodes[1];
        vm.SetSingleSelection(n2);
        Assert.Single(vm.SelectedNodes);
        Assert.Equal(n2, vm.SelectedNodes[0]);
    }

    [Fact]
    public async Task MindMapsViewModel_MultiNodeDrag_MovesAllSelectedNodes_And_UpdatesConnectedEdges()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Drag Map", null, null, 0, 0, DateTime.UtcNow, null));
        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Node A");
        vm.SelectedNode = null;
        await vm.AddNodeAsync("Node B");

        var nodeA = vm.CanvasNodes[0];
        var nodeB = vm.CanvasNodes[1];

        await vm.ConnectNodesAsync(nodeA.Id, nodeB.Id);
        Assert.Single(vm.CanvasEdges);
        var edge = vm.CanvasEdges[0];

        var origAX = nodeA.X;
        var origAY = nodeA.Y;
        var origBX = nodeB.X;
        var origBY = nodeB.Y;
        var origFromX = edge.SourceX;
        var origFromY = edge.SourceY;
        var origToX = edge.TargetX;
        var origToY = edge.TargetY;

        // Select both nodes and drag together
        vm.SelectAllCommand.Execute(null);
        vm.MoveSelectedNodes(50, 70);

        Assert.Equal(origAX + 50, nodeA.X);
        Assert.Equal(origAY + 70, nodeA.Y);
        Assert.Equal(origBX + 50, nodeB.X);
        Assert.Equal(origBY + 70, nodeB.Y);

        // Verify edge geometry followed the nodes
        Assert.Equal(origFromX + 50, edge.SourceX);
        Assert.Equal(origFromY + 70, edge.SourceY);
        Assert.Equal(origToX + 50, edge.TargetX);
        Assert.Equal(origToY + 70, edge.TargetY);

        // Undo
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(origAX, nodeA.X);
        Assert.Equal(origAY, nodeA.Y);
        Assert.Equal(origBX, nodeB.X);
        Assert.Equal(origBY, nodeB.Y);
        Assert.Equal(origFromX, edge.SourceX);
        Assert.Equal(origFromY, edge.SourceY);
        Assert.Equal(origToX, edge.TargetX);
        Assert.Equal(origToY, edge.TargetY);
    }

    [Fact]
    public async Task MindMapsViewModel_ResizeNode_ClampsToBounds_And_UpdatesEdges()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Resize Map", null, null, 0, 0, DateTime.UtcNow, null));
        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Parent");
        vm.SelectedNode = null;
        await vm.AddNodeAsync("Child");

        var p = vm.CanvasNodes[0];
        var c = vm.CanvasNodes[1];
        await vm.ConnectNodesAsync(p.Id, c.Id);

        var origW = p.Width;
        var origH = p.Height;

        // Resize below minimum bounds (min 80 width, min 40 height)
        vm.ResizeNode(p.Id, 50, 20);
        Assert.Equal(80, p.Width);
        Assert.Equal(40, p.Height);

        // Edge From point should update with new dimensions (center)
        var edge = vm.CanvasEdges[0];
        Assert.Equal(p.X + 40, edge.SourceX);
        Assert.Equal(p.Y + 20, edge.SourceY);

        // Undo restores original size
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(origW, p.Width);
        Assert.Equal(origH, p.Height);
    }

    [Fact]
    public async Task MindMapsViewModel_ConnectNodes_RejectsSelfAndDuplicateConnections()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Connect Validation Map", null, null, 0, 0, DateTime.UtcNow, null));
        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Node 1");
        vm.SelectedNode = null;
        await vm.AddNodeAsync("Node 2");

        var n1 = vm.CanvasNodes[0];
        var n2 = vm.CanvasNodes[1];

        // 1. Self connection must be rejected
        await vm.ConnectNodesAsync(n1.Id, n1.Id);
        Assert.Empty(vm.CanvasEdges);

        // 2. First valid connection succeeds
        await vm.ConnectNodesAsync(n1.Id, n2.Id);
        Assert.Single(vm.CanvasEdges);

        // 3. Duplicate connection must be rejected
        await vm.ConnectNodesAsync(n1.Id, n2.Id);
        Assert.Single(vm.CanvasEdges);

        // 4. Undo edge creation
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.CanvasEdges);

        // 5. Redo edge creation
        Assert.True(vm.UndoRedo.CanRedo);
        vm.RedoCommand.Execute(null);
        Assert.Single(vm.CanvasEdges);
    }

    [Fact]
    public async Task MindMapsViewModel_CopyPaste_And_DeleteSelected_WithEdge_SupportsUndo()
    {
        var mapId = Guid.NewGuid();
        _fakeApiClient.MindMaps.Add(new MindMapDto(mapId, _workspaceId, null, "Clipboard Map", null, null, 0, 0, DateTime.UtcNow, null));
        var vm = new MindMapsViewModel(_fakeApiClient, _userSession, _fakeNavigationService, _fakeDialogService);
        await vm.LoadMindMapsAsync();

        await vm.AddNodeAsync("Source Node");
        vm.SelectedNode = null;
        await vm.AddNodeAsync("Target Node");

        var n1 = vm.CanvasNodes[0];
        var n2 = vm.CanvasNodes[1];
        await vm.ConnectNodesAsync(n1.Id, n2.Id);
        Assert.Single(vm.CanvasEdges);

        // 1. Copy & Paste single node
        vm.SetSingleSelection(n1);
        vm.CopySelectedCommand.Execute(null);
        await vm.PasteAsync();
        Assert.Equal(3, vm.CanvasNodes.Count);

        var pastedNode = vm.CanvasNodes.Last();
        Assert.Equal(400, pastedNode.X);
        Assert.Equal(200, pastedNode.Y);

        // 2. Select all and Delete
        vm.SelectAllCommand.Execute(null);
        await vm.DeleteSelectedAsync();
        Assert.Empty(vm.CanvasNodes);
        Assert.Empty(vm.CanvasEdges);

        // 3. Undo restores deleted nodes and edge
        Assert.True(vm.UndoRedo.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(3, vm.CanvasNodes.Count);
        Assert.Single(vm.CanvasEdges);
    }

    #endregion
}
