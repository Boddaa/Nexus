using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Application.Features.VisualThinking.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;
using LLMRequest = Nexus.Application.Common.Models.LLMRequest;
using LLMResponse = Nexus.Application.Common.Models.LLMResponse;
using MindMapEdgeDto = Nexus.Application.DTOs.VisualThinking.MindMapEdgeDto;
using MindMapNodeDto = Nexus.Application.DTOs.VisualThinking.MindMapNodeDto;

namespace Nexus.Application.Tests;

public class VisualThinkingServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly User _testUser;
    private readonly User _otherUser;
    private readonly Workspace _workspaceA;
    private readonly Workspace _workspaceB;
    private readonly VisualThinkingOptions _options;
    private readonly BoardService _boardService;
    private readonly MindMapService _mindMapService;
    private readonly AutoLayoutService _autoLayoutService;
    private readonly FakeLlmService _fakeLlmService;
    private readonly AiMindMapService _aiMindMapService;

    private class FakeLlmService : ILLMService
    {
        public bool ShouldFail { get; set; } = false;
        public string ResponseToReturn { get; set; } = "{}";
        public LLMRequest? LastRequest { get; set; }

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldFail) throw new LlmException("Simulated LLM service failure");
            LastRequest = request;
            return Task.FromResult(new LLMResponse(ResponseToReturn, "fake-gpt-4o", 100, 100, 200));
        }
    }

    public VisualThinkingServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "VisualThinkingTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        _testUser = new User { Email = "alice@nexus.ai", FullName = "Alice Visual" };
        _otherUser = new User { Email = "bob@nexus.ai", FullName = "Bob Visual" };
        _context.Users.AddRange(_testUser, _otherUser);

        _workspaceA = new Workspace { Name = "Workspace A", OwnerId = _testUser.Id };
        _workspaceB = new Workspace { Name = "Workspace B", OwnerId = _otherUser.Id };
        _context.Workspaces.AddRange(_workspaceA, _workspaceB);

        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;

        _options = new VisualThinkingOptions
        {
            MaxBoardsPerWorkspace = 10,
            MaxBoardItemsPerBoard = 20,
            MaxMindMapsPerWorkspace = 10,
            MaxNodesPerMindMap = 20,
            MaxEdgesPerMindMap = 30,
            MaxBatchItems = 50,
            MaxBatchNodes = 50
        };

        var optionsWrapper = Options.Create(_options);

        _autoLayoutService = new AutoLayoutService();

        _boardService = new BoardService(
            _context,
            _currentUserService,
            optionsWrapper,
            NullLogger<BoardService>.Instance);

        _mindMapService = new MindMapService(
            _context,
            _currentUserService,
            _autoLayoutService,
            optionsWrapper,
            NullLogger<MindMapService>.Instance);

        _fakeLlmService = new FakeLlmService();

        _aiMindMapService = new AiMindMapService(
            _context,
            _currentUserService,
            _fakeLlmService,
            _autoLayoutService,
            _mindMapService,
            optionsWrapper,
            NullLogger<AiMindMapService>.Instance);
    }

    #region 1. Board & Board Items Tests

    [Fact]
    public async Task CreateBoard_CreatesBoard_InCurrentWorkspace()
    {
        var req = new CreateBoardRequest("Brainstorming Board", "Visual strategy", BoardType.Kanban, IsPersonal: false);
        var res = await _boardService.CreateBoardAsync(_workspaceA.Id, req);

        Assert.True(res.IsSuccess);
        Assert.Equal("Brainstorming Board", res.Value.Title);
        Assert.Equal(_workspaceA.Id, res.Value.WorkspaceId);
        Assert.Null(res.Value.UserId); // Shared
    }

    [Fact]
    public async Task GetBoards_EnforcesPersonalIsolation()
    {
        // 1. Shared board in Workspace A
        await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Shared Board", null, BoardType.Kanban, false));

        // 2. Personal board for Alice in Workspace A
        await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Alice Personal", null, BoardType.Kanban, true));

        // 3. Alice switches to Bob
        _currentUserService.UserId = _otherUser.Id;
        _currentUserService.Email = _otherUser.Email;

        // Bob creates his personal board in Workspace A
        // Give Bob membership in Workspace A
        _context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = _workspaceA.Id, UserId = _otherUser.Id, Role = WorkspaceRole.Editor });
        await _context.SaveChangesAsync();

        await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Bob Personal", null, BoardType.Kanban, true));

        // Bob queries boards
        var bobBoardsRes = await _boardService.GetBoardsAsync(_workspaceA.Id);
        Assert.True(bobBoardsRes.IsSuccess);
        Assert.Equal(2, bobBoardsRes.Value.Count); // Shared Board + Bob Personal (Alice's personal is hidden)
        Assert.Contains(bobBoardsRes.Value, b => b.Title == "Shared Board");
        Assert.Contains(bobBoardsRes.Value, b => b.Title == "Bob Personal");
        Assert.DoesNotContain(bobBoardsRes.Value, b => b.Title == "Alice Personal");
    }

    [Fact]
    public async Task GetBoardById_RejectsCrossWorkspaceAccess()
    {
        var createRes = await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Alpha Board", null, BoardType.Kanban, false));
        Assert.True(createRes.IsSuccess);

        // Try to access Alpha Board through Workspace B (where Alice is not an owner or member)
        var accessRes = await _boardService.GetBoardByIdAsync(_workspaceB.Id, createRes.Value.Id);
        Assert.False(accessRes.IsSuccess);
        Assert.Equal("Workspace.AccessDenied", accessRes.Error.Code);
    }

    [Fact]
    public async Task DeleteBoard_SoftDeletesBoardAndItems()
    {
        var boardRes = await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("To Delete", null, BoardType.Kanban, false));
        var boardId = boardRes.Value.Id;

        var itemRes = await _boardService.CreateBoardItemAsync(_workspaceA.Id, boardId, new CreateBoardItemRequest(
            BoardItemType.StickyNote, "Sticky 1", X: 10, Y: 10, Width: 100, Height: 100, ColorHex: "#FFFF00", Content: "Content"));
        Assert.True(itemRes.IsSuccess);

        var delRes = await _boardService.DeleteBoardAsync(_workspaceA.Id, boardId);
        Assert.True(delRes.IsSuccess);

        var getRes = await _boardService.GetBoardByIdAsync(_workspaceA.Id, boardId);
        Assert.False(getRes.IsSuccess);

        var itemsRes = await _boardService.GetBoardItemsAsync(_workspaceA.Id, boardId);
        Assert.False(itemsRes.IsSuccess);
    }

    [Fact]
    public async Task CreateBoardItem_EnforcesCapacityLimit()
    {
        var boardRes = await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Capacity Test", null, BoardType.Kanban, false));
        var boardId = boardRes.Value.Id;

        // Max is configured to 20
        for (int i = 0; i < 20; i++)
        {
            var res = await _boardService.CreateBoardItemAsync(_workspaceA.Id, boardId, new CreateBoardItemRequest(
                BoardItemType.StickyNote, $"Note {i}", X: i * 10, Y: i * 10, Width: 100, Height: 100, ColorHex: "#FFF"));
            Assert.True(res.IsSuccess);
        }

        // 21st item should fail
        var overflowRes = await _boardService.CreateBoardItemAsync(_workspaceA.Id, boardId, new CreateBoardItemRequest(
            BoardItemType.StickyNote, "Overflow Note", X: 100, Y: 100, Width: 100, Height: 100, ColorHex: "#FFF"));

        Assert.False(overflowRes.IsSuccess);
        Assert.Equal("BoardItem.LimitExceeded", overflowRes.Error.Code);
    }

    [Fact]
    public async Task UpdateBoardItem_ValidatesBoardMismatch()
    {
        var board1 = await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Board 1", null, BoardType.Kanban, false));
        var board2 = await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Board 2", null, BoardType.Kanban, false));

        var item = await _boardService.CreateBoardItemAsync(_workspaceA.Id, board1.Value.Id, new CreateBoardItemRequest(
            BoardItemType.StickyNote, "Note", X: 0, Y: 0, Width: 100, Height: 100, ColorHex: "#FFF"));

        // Attempt to update item on board 2
        var updateRes = await _boardService.UpdateBoardItemAsync(_workspaceA.Id, board2.Value.Id, item.Value.Id, new UpdateBoardItemRequest(Title: "New Title"));
        Assert.False(updateRes.IsSuccess);
        Assert.Equal("BoardItem.Mismatch", updateRes.Error.Code);
    }

    [Fact]
    public async Task BatchUpdateBoardItems_UpdatesPositionsAtomically()
    {
        var board = await _boardService.CreateBoardAsync(_workspaceA.Id, new CreateBoardRequest("Batch Board", null, BoardType.Kanban, false));
        var item1 = await _boardService.CreateBoardItemAsync(_workspaceA.Id, board.Value.Id, new CreateBoardItemRequest(
            BoardItemType.StickyNote, "Note 1", X: 10, Y: 10, Width: 100, Height: 100, ColorHex: "#FFF"));
        var item2 = await _boardService.CreateBoardItemAsync(_workspaceA.Id, board.Value.Id, new CreateBoardItemRequest(
            BoardItemType.Shape, "Shape 2", X: 20, Y: 20, Width: 150, Height: 150, ColorHex: "#000"));

        var batchReq = new BatchUpdateBoardItemsRequest(new List<BoardItemBatchPositionDto>
        {
            new(item1.Value.Id, 250, 350, 120, 120, 45, 5),
            new(item2.Value.Id, 500, 600, 200, 200, 0, 6)
        });

        var batchRes = await _boardService.BatchUpdateBoardItemsAsync(_workspaceA.Id, board.Value.Id, batchReq);
        Assert.True(batchRes.IsSuccess);

        var itemsRes = await _boardService.GetBoardItemsAsync(_workspaceA.Id, board.Value.Id);
        var updated1 = itemsRes.Value.First(i => i.Id == item1.Value.Id);
        var updated2 = itemsRes.Value.First(i => i.Id == item2.Value.Id);

        Assert.Equal(250, updated1.X);
        Assert.Equal(350, updated1.Y);
        Assert.Equal(45, updated1.Rotation);
        Assert.Equal(5, updated1.ZIndex);

        Assert.Equal(500, updated2.X);
        Assert.Equal(600, updated2.Y);
        Assert.Equal(6, updated2.ZIndex);
    }

    #endregion

    #region 2. Mind Map & Graph Integrity Tests

    [Fact]
    public async Task CreateMindMap_CreatesMap_AndSupportsSnapshot()
    {
        var mapRes = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("System Architecture", "Graph view"));
        Assert.True(mapRes.IsSuccess);

        var snapshotRes = await _mindMapService.GetMindMapByIdAsync(_workspaceA.Id, mapRes.Value.Id);
        Assert.True(snapshotRes.IsSuccess);
        Assert.Equal("System Architecture", snapshotRes.Value.Title);
        Assert.Empty(snapshotRes.Value.Nodes);
        Assert.Empty(snapshotRes.Value.Edges);
    }

    [Fact]
    public async Task CreateEdge_RejectsSelfEdge()
    {
        var mapRes = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Self Edge Test"));
        var mapId = mapRes.Value.Id;

        var nodeRes = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("Node 1", X: 100, Y: 100));
        Assert.True(nodeRes.IsSuccess);

        var edgeRes = await _mindMapService.CreateEdgeAsync(_workspaceA.Id, mapId, new CreateMindMapEdgeRequest(
            nodeRes.Value.Id, nodeRes.Value.Id, "Connects to self"));

        Assert.False(edgeRes.IsSuccess);
        Assert.Equal("MindMapEdge.SelfEdgeProhibited", edgeRes.Error.Code);
    }

    [Fact]
    public async Task CreateEdge_RejectsCrossMapEdges()
    {
        var map1 = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Map 1"));
        var map2 = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Map 2"));

        var node1 = await _mindMapService.CreateNodeAsync(_workspaceA.Id, map1.Value.Id, new CreateMindMapNodeRequest("Node in Map 1", X: 100, Y: 100));
        var node2 = await _mindMapService.CreateNodeAsync(_workspaceA.Id, map2.Value.Id, new CreateMindMapNodeRequest("Node in Map 2", X: 200, Y: 200));

        // Attempt to create edge in Map 1 pointing to Node 2 (which is in Map 2)
        var edgeRes = await _mindMapService.CreateEdgeAsync(_workspaceA.Id, map1.Value.Id, new CreateMindMapEdgeRequest(
            node1.Value.Id, node2.Value.Id));

        Assert.False(edgeRes.IsSuccess);
        Assert.Equal("MindMapEdge.CrossMapProhibited", edgeRes.Error.Code);
    }

    [Fact]
    public async Task CreateEdge_RejectsDuplicateEdges()
    {
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Duplicate Test"));
        var mapId = map.Value.Id;

        var nodeA = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("A", X: 100, Y: 100));
        var nodeB = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("B", X: 200, Y: 200));

        var edge1 = await _mindMapService.CreateEdgeAsync(_workspaceA.Id, mapId, new CreateMindMapEdgeRequest(nodeA.Value.Id, nodeB.Value.Id, "leads to"));
        Assert.True(edge1.IsSuccess);

        // Attempt duplicate edge
        var edge2 = await _mindMapService.CreateEdgeAsync(_workspaceA.Id, mapId, new CreateMindMapEdgeRequest(nodeA.Value.Id, nodeB.Value.Id, "leads to again"));
        Assert.False(edge2.IsSuccess);
        Assert.Equal("MindMapEdge.Duplicate", edge2.Error.Code);
    }

    [Fact]
    public async Task CreateEdge_RejectsSoftDeletedNode()
    {
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Soft Delete Edge Test"));
        var mapId = map.Value.Id;

        var nodeA = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("A", X: 100, Y: 100));
        var nodeB = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("B", X: 200, Y: 200));

        // Delete node B
        await _mindMapService.DeleteNodeAsync(_workspaceA.Id, mapId, nodeB.Value.Id);

        // Attempt edge to deleted node
        var edgeRes = await _mindMapService.CreateEdgeAsync(_workspaceA.Id, mapId, new CreateMindMapEdgeRequest(nodeA.Value.Id, nodeB.Value.Id));
        Assert.False(edgeRes.IsSuccess);
        Assert.Equal("MindMapEdge.TargetNotFound", edgeRes.Error.Code);
    }

    [Fact]
    public async Task DeleteNode_CascadeSoftDeletesConnectedEdges()
    {
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Cascade Test"));
        var mapId = map.Value.Id;

        var nodeA = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("A", X: 100, Y: 100));
        var nodeB = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("B", X: 200, Y: 200));
        var edge = await _mindMapService.CreateEdgeAsync(_workspaceA.Id, mapId, new CreateMindMapEdgeRequest(nodeA.Value.Id, nodeB.Value.Id));
        Assert.True(edge.IsSuccess);

        // Delete node A
        var delRes = await _mindMapService.DeleteNodeAsync(_workspaceA.Id, mapId, nodeA.Value.Id);
        Assert.True(delRes.IsSuccess);

        // Snapshot should show 1 node (B) and 0 edges
        var snapshot = await _mindMapService.GetMindMapByIdAsync(_workspaceA.Id, mapId);
        Assert.Single(snapshot.Value.Nodes);
        Assert.Equal(nodeB.Value.Id, snapshot.Value.Nodes[0].Id);
        Assert.Empty(snapshot.Value.Edges);
    }

    [Fact]
    public async Task BatchUpdateNodes_UpdatesMultipleNodePositions()
    {
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Batch Nodes"));
        var mapId = map.Value.Id;

        var node1 = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("N1", X: 10, Y: 10));
        var node2 = await _mindMapService.CreateNodeAsync(_workspaceA.Id, mapId, new CreateMindMapNodeRequest("N2", X: 20, Y: 20));

        var batchReq = new BatchUpdateMindMapNodesRequest(new List<MindMapNodeBatchPositionDto>
        {
            new(node1.Value.Id, 333, 444, 220, 90),
            new(node2.Value.Id, 555, 666, 250, 100)
        });

        var res = await _mindMapService.BatchUpdateNodesAsync(_workspaceA.Id, mapId, batchReq);
        Assert.True(res.IsSuccess);

        var snapshot = await _mindMapService.GetMindMapByIdAsync(_workspaceA.Id, mapId);
        var n1 = snapshot.Value.Nodes.First(n => n.Id == node1.Value.Id);
        var n2 = snapshot.Value.Nodes.First(n => n.Id == node2.Value.Id);

        Assert.Equal(333, n1.X);
        Assert.Equal(444, n1.Y);
        Assert.Equal(220, n1.Width);

        Assert.Equal(555, n2.X);
        Assert.Equal(666, n2.Y);
        Assert.Equal(250, n2.Width);
    }

    #endregion

    #region 3. Auto-Layout Determinism Tests

    [Fact]
    public async Task AutoLayout_IsDeterministic_AcrossMultipleRuns()
    {
        var rootId = Guid.NewGuid();
        var child1Id = Guid.NewGuid();
        var child2Id = Guid.NewGuid();
        var grandChildId = Guid.NewGuid();

        var nodes = new List<MindMapNodeDto>
        {
            new(rootId, Guid.NewGuid(), null, "Root", null, 0, 0, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null),
            new(child1Id, Guid.NewGuid(), rootId, "Child 1", null, 0, 0, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null),
            new(child2Id, Guid.NewGuid(), rootId, "Child 2", null, 0, 0, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null),
            new(grandChildId, Guid.NewGuid(), child1Id, "Grand Child", null, 0, 0, 180, 80, "#FFF", "rectangle", MindMapNodeType.Concept, null, null, DateTime.UtcNow, null),
        };

        var edges = new List<MindMapEdgeDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), rootId, child1Id, null, null, "Solid", MindMapEdgeType.RelatesTo, DateTime.UtcNow, null),
            new(Guid.NewGuid(), Guid.NewGuid(), rootId, child2Id, null, null, "Solid", MindMapEdgeType.RelatesTo, DateTime.UtcNow, null),
            new(Guid.NewGuid(), Guid.NewGuid(), child1Id, grandChildId, null, null, "Solid", MindMapEdgeType.RelatesTo, DateTime.UtcNow, null),
        };

        // Run 1: Vertical Tree
        var run1Vertical = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, new ApplyLayoutRequest(AutoLayoutAlgorithm.VerticalTree));
        var run2Vertical = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, new ApplyLayoutRequest(AutoLayoutAlgorithm.VerticalTree));

        Assert.True(run1Vertical.IsSuccess);
        Assert.True(run2Vertical.IsSuccess);
        Assert.Equal(run1Vertical.Value.Positions.Count, run2Vertical.Value.Positions.Count);
        foreach (var r1 in run1Vertical.Value.Positions)
        {
            var r2 = run2Vertical.Value.Positions.First(x => x.NodeId == r1.NodeId);
            Assert.Equal(r1.X, r2.X);
            Assert.Equal(r1.Y, r2.Y);
        }

        // Run 2: Horizontal Tree
        var run1Horizontal = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, new ApplyLayoutRequest(AutoLayoutAlgorithm.HorizontalTree));
        var run2Horizontal = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, new ApplyLayoutRequest(AutoLayoutAlgorithm.HorizontalTree));

        Assert.True(run1Horizontal.IsSuccess);
        Assert.True(run2Horizontal.IsSuccess);
        Assert.Equal(run1Horizontal.Value.Positions.Count, run2Horizontal.Value.Positions.Count);
        foreach (var r1 in run1Horizontal.Value.Positions)
        {
            var r2 = run2Horizontal.Value.Positions.First(x => x.NodeId == r1.NodeId);
            Assert.Equal(r1.X, r2.X);
            Assert.Equal(r1.Y, r2.Y);
        }

        // Run 3: Radial
        var run1Radial = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, new ApplyLayoutRequest(AutoLayoutAlgorithm.Radial));
        var run2Radial = await _autoLayoutService.ApplyLayoutAsync(nodes, edges, new ApplyLayoutRequest(AutoLayoutAlgorithm.Radial));

        Assert.True(run1Radial.IsSuccess);
        Assert.True(run2Radial.IsSuccess);
        Assert.Equal(run1Radial.Value.Positions.Count, run2Radial.Value.Positions.Count);
        foreach (var r1 in run1Radial.Value.Positions)
        {
            var r2 = run2Radial.Value.Positions.First(x => x.NodeId == r1.NodeId);
            Assert.Equal(r1.X, r2.X);
            Assert.Equal(r1.Y, r2.Y);
        }
    }

    #endregion

    #region 4. Knowledge Context Linking Tests

    [Fact]
    public async Task GetNodeKnowledgeContext_ReturnsContext_WhenLinkedToPage()
    {
        // 1. Create a Page
        var page = new Page
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Distributed Caching Guide",
            Icon = "📖",
            ContentJson = "{\"blocks\":[{\"text\":\"Redis provides in-memory key-value cache.\"}]}"
        };
        _context.Pages.Add(page);
        await _context.SaveChangesAsync();

        // 2. Create Mind Map & Node linked to Page
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Cache Map"));
        var node = await _mindMapService.CreateNodeAsync(_workspaceA.Id, map.Value.Id, new CreateMindMapNodeRequest(
            Title: "Caching Layer",
            X: 100,
            Y: 100,
            LinkedEntityType: "Page",
            LinkedEntityId: page.Id));
        Assert.True(node.IsSuccess);

        // 3. Resolve context
        var ctxRes = await _mindMapService.GetNodeKnowledgeContextAsync(_workspaceA.Id, map.Value.Id, node.Value.Id);
        Assert.True(ctxRes.IsSuccess);
        Assert.Equal(node.Value.Id, ctxRes.Value.NodeId);
        Assert.Equal("Distributed Caching Guide", ctxRes.Value.EntityTitle);
        Assert.Equal("Page", ctxRes.Value.EntityType);
        Assert.Contains("Redis provides in-memory", ctxRes.Value.Snippet);
    }

    [Fact]
    public async Task GetNodeKnowledgeContext_Fails_WhenNoEntityLinked()
    {
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("No Link Map"));
        var node = await _mindMapService.CreateNodeAsync(_workspaceA.Id, map.Value.Id, new CreateMindMapNodeRequest("Unlinked Node", X: 100, Y: 100));

        var ctxRes = await _mindMapService.GetNodeKnowledgeContextAsync(_workspaceA.Id, map.Value.Id, node.Value.Id);
        Assert.False(ctxRes.IsSuccess);
        Assert.Equal("MindMapNode.NoKnowledgeLink", ctxRes.Error.Code);
    }

    #endregion

    #region 5. AI Mind Map Service Tests

    [Fact]
    public async Task GenerateMindMap_ParsesLlmJson_AndCreatesAuthoritativeGraph()
    {
        // LLM structured response with temporary IDs: "root", "c1", "c2"
        var aiResponse = @"
{
  ""title"": ""Clean Architecture"",
  ""description"": ""Domain Centric Design"",
  ""nodes"": [
    { ""temporaryId"": ""root"", ""title"": ""Clean Architecture"", ""nodeType"": ""Concept"", ""colorHex"": ""#8B5CF6"" },
    { ""temporaryId"": ""c1"", ""title"": ""Domain Layer"", ""nodeType"": ""Concept"", ""colorHex"": ""#3B82F6"" },
    { ""temporaryId"": ""c2"", ""title"": ""Application Layer"", ""nodeType"": ""Concept"", ""colorHex"": ""#10B981"" }
  ],
  ""edges"": [
    { ""source"": ""root"", ""target"": ""c1"", ""label"": ""core"" },
    { ""source"": ""c1"", ""target"": ""c2"", ""label"": ""orchestrates"" }
  ]
}";
        _fakeLlmService.ResponseToReturn = aiResponse;

        var genRes = await _aiMindMapService.GenerateMindMapAsync(_workspaceA.Id, new GenerateMindMapRequest("Clean Architecture", MaxNodes: 10));
        Assert.True(genRes.IsSuccess);
        Assert.Equal("Clean Architecture", genRes.Value.Title);
        Assert.Equal(3, genRes.Value.NodeCount);
        Assert.Equal(2, genRes.Value.EdgeCount);

        // Verify authoritative database persistence
        var snapshot = await _mindMapService.GetMindMapByIdAsync(_workspaceA.Id, genRes.Value.MindMapId);
        Assert.True(snapshot.IsSuccess);
        Assert.Equal(3, snapshot.Value.Nodes.Count);
        Assert.Equal(2, snapshot.Value.Edges.Count);

        // Verify Provenance is saved
        var aiGen = await _context.AiGenerations.FirstOrDefaultAsync(g => g.WorkspaceId == _workspaceA.Id && g.Operation == "MindMapGeneration");
        Assert.NotNull(aiGen);
        Assert.Contains("Clean Architecture", aiGen.Content);
    }

    [Fact]
    public async Task GenerateMindMap_FailsGracefully_OnInvalidJson()
    {
        _fakeLlmService.ResponseToReturn = "This is not valid JSON at all.";

        var genRes = await _aiMindMapService.GenerateMindMapAsync(_workspaceA.Id, new GenerateMindMapRequest("Invalid Prompt"));
        Assert.False(genRes.IsSuccess);
        Assert.Equal("AiMindMap.InvalidJson", genRes.Error.Code);
    }

    [Fact]
    public async Task ExplainNode_CallsLlm_AndReturnsExplanation()
    {
        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Explain Map"));
        var node = await _mindMapService.CreateNodeAsync(_workspaceA.Id, map.Value.Id, new CreateMindMapNodeRequest("Event Sourcing", X: 100, Y: 100));

        var explainText = @"Event Sourcing persists state changes as an append-only event log.
Instead of updating a row in place, every state change is an immutable event object.

- Complete audit trail
- Time-travel debugging
- High write throughput";

        _fakeLlmService.ResponseToReturn = explainText;

        var expRes = await _aiMindMapService.ExplainNodeAsync(_workspaceA.Id, map.Value.Id, node.Value.Id, new ExplainNodeRequest());
        Assert.True(expRes.IsSuccess);
        Assert.Equal("Event Sourcing", expRes.Value.NodeTitle);
        Assert.Contains("append-only event log", expRes.Value.Explanation);
        Assert.Equal(3, expRes.Value.KeyTakeaways.Count);
    }

    [Fact]
    public async Task FindRelatedKnowledge_FindsMatchingWorkspaceContent()
    {
        // Create note and page with matching keyword
        var page = new Page
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Kubernetes Deployment Guide",
            Icon = "☸️",
            ContentJson = "{\"blocks\":[{\"text\":\"Configuring ingress and service mesh.\"}]}"
        };
        _context.Pages.Add(page);
        await _context.SaveChangesAsync();

        var map = await _mindMapService.CreateMindMapAsync(_workspaceA.Id, new CreateMindMapRequest("Infra Map"));
        var node = await _mindMapService.CreateNodeAsync(_workspaceA.Id, map.Value.Id, new CreateMindMapNodeRequest("Kubernetes Cluster", X: 100, Y: 100));

        var relRes = await _aiMindMapService.FindRelatedKnowledgeAsync(_workspaceA.Id, map.Value.Id, node.Value.Id, new FindRelatedKnowledgeRequest());
        Assert.True(relRes.IsSuccess);
        Assert.NotEmpty(relRes.Value.RelatedItems);
        Assert.Contains(relRes.Value.RelatedItems, i => i.Title.Contains("Kubernetes"));
    }

    #endregion
}
