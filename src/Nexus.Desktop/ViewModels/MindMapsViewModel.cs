using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;
using Nexus.Domain.Enums;

namespace Nexus.Desktop.ViewModels;

public partial class MindMapsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly UserSession _userSession;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _debounceTimer;
    private readonly HashSet<Guid> _pendingPersistNodeIds = new();

    [ObservableProperty]
    private ObservableCollection<MindMapDto> _mindMaps = new();

    [ObservableProperty]
    private MindMapDto? _selectedMindMap;

    [ObservableProperty]
    private ObservableCollection<CanvasNodeViewModel> _canvasNodes = new();

    [ObservableProperty]
    private ObservableCollection<CanvasEdgeViewModel> _canvasEdges = new();

    [ObservableProperty]
    private CanvasNodeViewModel? _selectedNode;

    [ObservableProperty]
    private CanvasEdgeViewModel? _selectedEdge;

    [ObservableProperty]
    private CanvasTool _activeTool = CanvasTool.Select;

    [ObservableProperty]
    private Guid? _connectingSourceNodeId;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private double _panX = 0;

    [ObservableProperty]
    private double _panY = 0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    // AI Drawer & Panels
    [ObservableProperty]
    private bool _isAiPanelOpen;

    [ObservableProperty]
    private string _aiPanelTitle = "AI Mind Map Studio";

    [ObservableProperty]
    private string _aiExplanationText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _aiKeyTakeaways = new();

    [ObservableProperty]
    private ObservableCollection<RelatedKnowledgeItemDto> _relatedKnowledgeItems = new();

    [ObservableProperty]
    private string _newMindMapTitle = string.Empty;

    [ObservableProperty]
    private string _newNodeTitle = string.Empty;

    [ObservableProperty]
    private string _aiPromptText = string.Empty;

    public CanvasUndoRedoManager UndoRedo { get; } = new();

    public string ZoomPercentageText => $"{Math.Round(ZoomLevel * 100)}%";

    public MindMapsViewModel(
        IApiClient apiClient,
        UserSession userSession,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _apiClient = apiClient;
        _userSession = userSession;
        _navigationService = navigationService;
        _dialogService = dialogService;

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += OnDebounceTimerTick;

        _ = LoadMindMapsAsync();
    }

    [RelayCommand]
    public async Task LoadMindMapsAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoading = true;
        StatusMessage = "Loading mind maps...";
        try
        {
            var res = await _apiClient.GetMindMapsAsync(ws.Id);
            if (res.IsSuccess)
            {
                MindMaps.Clear();
                foreach (var m in res.Value) MindMaps.Add(m);

                if (MindMaps.Count > 0 && SelectedMindMap == null)
                {
                    SelectedMindMap = MindMaps[0];
                    await LoadMindMapDetailsAsync(SelectedMindMap.Id);
                }
                StatusMessage = $"{MindMaps.Count} mind map(s) loaded.";
            }
            else
            {
                StatusMessage = $"Error: {res.Error.Description}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load mind maps: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SelectMindMapAsync(MindMapDto? map)
    {
        if (map == null) return;
        SelectedMindMap = map;
        await LoadMindMapDetailsAsync(map.Id);
    }

    private async Task LoadMindMapDetailsAsync(Guid mindMapId)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoading = true;
        try
        {
            var res = await _apiClient.GetMindMapByIdAsync(ws.Id, mindMapId);
            if (res.IsSuccess)
            {
                CanvasNodes.Clear();
                CanvasEdges.Clear();
                UndoRedo.Clear();

                var nodeMap = new Dictionary<Guid, CanvasNodeViewModel>();
                foreach (var n in res.Value.Nodes)
                {
                    var nodeVm = new CanvasNodeViewModel
                    {
                        Id = n.Id,
                        MindMapId = n.MindMapId,
                        ParentNodeId = n.ParentNodeId,
                        Title = n.Title,
                        Description = n.Description,
                        X = n.X,
                        Y = n.Y,
                        Width = n.Width > 0 ? n.Width : 180,
                        Height = n.Height > 0 ? n.Height : 80,
                        ColorHex = n.ColorHex,
                        Shape = n.Shape,
                        NodeType = n.NodeType,
                        LinkedEntityType = n.LinkedEntityType,
                        LinkedEntityId = n.LinkedEntityId,
                        IsRoot = n.Id == res.Value.RootNodeId
                    };
                    CanvasNodes.Add(nodeVm);
                    nodeMap[n.Id] = nodeVm;
                }

                foreach (var e in res.Value.Edges)
                {
                    var src = nodeMap.GetValueOrDefault(e.SourceNodeId);
                    var tgt = nodeMap.GetValueOrDefault(e.TargetNodeId);
                    if (src != null && tgt != null)
                    {
                        CanvasEdges.Add(new CanvasEdgeViewModel
                        {
                            Id = e.Id,
                            MindMapId = e.MindMapId,
                            SourceNodeId = e.SourceNodeId,
                            TargetNodeId = e.TargetNodeId,
                            SourceX = src.CenterX,
                            SourceY = src.CenterY,
                            TargetX = tgt.CenterX,
                            TargetY = tgt.CenterY,
                            Label = e.Label,
                            RelationType = e.RelationType,
                            Style = e.Style,
                            EdgeType = e.EdgeType
                        });
                    }
                }

                SelectedNode = CanvasNodes.FirstOrDefault();
                StatusMessage = $"Mind Map '{res.Value.Title}' loaded ({CanvasNodes.Count} nodes, {CanvasEdges.Count} edges).";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CreateMindMapAsync(string? titleParam = null)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var title = !string.IsNullOrWhiteSpace(titleParam)
            ? titleParam.Trim()
            : (!string.IsNullOrWhiteSpace(NewMindMapTitle) ? NewMindMapTitle.Trim() : $"Mind Map {MindMaps.Count + 1}");
        NewMindMapTitle = string.Empty;

        var res = await _apiClient.CreateMindMapAsync(ws.Id, new CreateMindMapRequest(title));
        if (res.IsSuccess)
        {
            MindMaps.Insert(0, res.Value);
            SelectedMindMap = res.Value;
            CanvasNodes.Clear();
            CanvasEdges.Clear();
            UndoRedo.Clear();

            // Auto-create a root concept node
            var nodeReq = new CreateMindMapNodeRequest(
                Title: res.Value.Title,
                X: 400,
                Y: 150,
                Width: 200,
                Height: 80,
                ColorHex: "#8B5CF6"
            );
            var nodeRes = await _apiClient.CreateMindMapNodeAsync(ws.Id, res.Value.Id, nodeReq);
            if (nodeRes.IsSuccess)
            {
                var n = nodeRes.Value;
                var rootNodeVm = new CanvasNodeViewModel
                {
                    Id = n.Id,
                    MindMapId = n.MindMapId,
                    Title = n.Title,
                    X = n.X,
                    Y = n.Y,
                    Width = n.Width,
                    Height = n.Height,
                    ColorHex = n.ColorHex,
                    Shape = n.Shape,
                    NodeType = n.NodeType,
                    IsRoot = true,
                    IsSelected = true
                };
                CanvasNodes.Add(rootNodeVm);
                SelectedNode = rootNodeVm;
            }

            StatusMessage = $"Created mind map '{res.Value.Title}'.";
        }
        else
        {
            StatusMessage = $"Error: {res.Error.Description}";
        }
    }

    [RelayCommand]
    public async Task DeleteCurrentMindMapAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null) return;

        var confirm = await _dialogService.ConfirmAsync("Delete Mind Map", $"Are you sure you want to delete '{SelectedMindMap.Title}'?");
        if (!confirm) return;

        var res = await _apiClient.DeleteMindMapAsync(ws.Id, SelectedMindMap.Id);
        if (res.IsSuccess)
        {
            MindMaps.Remove(SelectedMindMap);
            SelectedMindMap = MindMaps.FirstOrDefault();
            if (SelectedMindMap != null)
            {
                await LoadMindMapDetailsAsync(SelectedMindMap.Id);
            }
            else
            {
                CanvasNodes.Clear();
                CanvasEdges.Clear();
            }
            StatusMessage = "Mind map deleted.";
        }
    }

    // ==================== Node & Edge Operations ====================

    [RelayCommand]
    public async Task AddNodeAsync(string? titleParam = null)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null) return;

        var title = !string.IsNullOrWhiteSpace(titleParam)
            ? titleParam.Trim()
            : (!string.IsNullOrWhiteSpace(NewNodeTitle) ? NewNodeTitle.Trim() : (SelectedNode != null ? $"Child Concept {CanvasNodes.Count + 1}" : $"Concept {CanvasNodes.Count + 1}"));
        NewNodeTitle = string.Empty;

        var parentNode = SelectedNode;
        var spawnX = parentNode != null ? parentNode.X + 220 : 400 - PanX;
        var spawnY = parentNode != null ? parentNode.Y + (CanvasNodes.Count * 25 % 150) : 250 - PanY;

        var req = new CreateMindMapNodeRequest(
            Title: title.Trim(),
            ParentNodeId: parentNode?.Id,
            X: Math.Max(20, spawnX),
            Y: Math.Max(20, spawnY),
            Width: 180,
            Height: 80,
            ColorHex: "#3B82F6"
        );

        var res = await _apiClient.CreateMindMapNodeAsync(ws.Id, SelectedMindMap.Id, req);
        if (res.IsSuccess)
        {
            var n = res.Value;
            var nodeVm = new CanvasNodeViewModel
            {
                Id = n.Id,
                MindMapId = n.MindMapId,
                ParentNodeId = n.ParentNodeId,
                Title = n.Title,
                Description = n.Description,
                X = n.X,
                Y = n.Y,
                Width = n.Width,
                Height = n.Height,
                ColorHex = n.ColorHex,
                Shape = n.Shape,
                NodeType = n.NodeType,
                IsSelected = true
            };

            foreach (var node in CanvasNodes) node.IsSelected = false;
            CanvasNodes.Add(nodeVm);
            SelectedNode = nodeVm;

            // Automatically connect to parent if parent was selected
            if (parentNode != null)
            {
                await ConnectNodesAsync(parentNode.Id, nodeVm.Id, "relates to");
            }

            StatusMessage = $"Added concept '{n.Title}'.";
        }
    }

    public IReadOnlyList<CanvasNodeViewModel> SelectedNodes => CanvasNodes.Where(n => n.IsSelected).ToList();

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var n in CanvasNodes) n.IsSelected = true;
        if (SelectedNode == null) SelectedNode = CanvasNodes.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedNodes));
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var n in CanvasNodes) n.IsSelected = false;
        foreach (var edge in CanvasEdges) edge.IsSelected = false;
        SelectedNode = null;
        SelectedEdge = null;
        ConnectingSourceNodeId = null;
        OnPropertyChanged(nameof(SelectedNodes));
    }

    public void ToggleNodeSelection(CanvasNodeViewModel node)
    {
        node.IsSelected = !node.IsSelected;
        if (node.IsSelected && SelectedNode == null) SelectedNode = node;
        else if (!node.IsSelected && SelectedNode == node) SelectedNode = CanvasNodes.FirstOrDefault(n => n.IsSelected);
        OnPropertyChanged(nameof(SelectedNodes));
    }

    public void SetSingleSelection(CanvasNodeViewModel node)
    {
        foreach (var n in CanvasNodes) n.IsSelected = (n.Id == node.Id);
        foreach (var edge in CanvasEdges) edge.IsSelected = false;
        SelectedNode = node;
        SelectedEdge = null;
        OnPropertyChanged(nameof(SelectedNodes));
    }

    [RelayCommand]
    public async Task DeleteSelectedNodeAsync()
    {
        await DeleteSelectedAsync();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null) return;

        var selected = SelectedNodes;
        if (selected.Count == 0 && SelectedNode != null)
        {
            selected = new[] { SelectedNode };
        }
        if (selected.Count == 0) return;

        int deletedCount = 0;
        var deletedNodes = new List<CanvasNodeViewModel>();
        var deletedEdges = new List<CanvasEdgeViewModel>();
        foreach (var node in selected.ToList())
        {
            var res = await _apiClient.DeleteMindMapNodeAsync(ws.Id, SelectedMindMap.Id, node.Id);
            if (res.IsSuccess)
            {
                CanvasNodes.Remove(node);
                deletedNodes.Add(node);
                var edgesToRemove = CanvasEdges.Where(e => e.SourceNodeId == node.Id || e.TargetNodeId == node.Id).ToList();
                foreach (var edge in edgesToRemove)
                {
                    CanvasEdges.Remove(edge);
                    if (!deletedEdges.Contains(edge)) deletedEdges.Add(edge);
                }
                deletedCount++;
            }
        }

        if (deletedNodes.Count > 0)
        {
            UndoRedo.PushAlreadyExecuted(new DeleteCanvasItemAction<(IReadOnlyList<CanvasNodeViewModel> nodes, IReadOnlyList<CanvasEdgeViewModel> edges)>(
                (deletedNodes, deletedEdges),
                tuple =>
                {
                    foreach (var n in tuple.nodes) if (!CanvasNodes.Contains(n)) CanvasNodes.Add(n);
                    foreach (var e in tuple.edges) if (!CanvasEdges.Contains(e)) CanvasEdges.Add(e);
                },
                tuple =>
                {
                    foreach (var n in tuple.nodes) CanvasNodes.Remove(n);
                    foreach (var e in tuple.edges) CanvasEdges.Remove(e);
                }
            ));
        }

        SelectedNode = CanvasNodes.LastOrDefault();
        StatusMessage = $"Deleted {deletedCount} concept node(s).";
    }

    private static readonly List<ClipboardNodeData> _clipboard = new();

    [RelayCommand]
    public void CopySelected()
    {
        var selected = SelectedNodes;
        if (selected.Count == 0 && SelectedNode != null)
        {
            selected = new[] { SelectedNode };
        }
        if (selected.Count == 0) return;

        _clipboard.Clear();
        foreach (var n in selected)
        {
            _clipboard.Add(new ClipboardNodeData(
                n.Title,
                n.Description,
                n.Width,
                n.Height,
                n.ColorHex,
                n.Shape,
                n.NodeType,
                n.LinkedEntityType,
                n.LinkedEntityId
            ));
        }
        StatusMessage = $"Copied {_clipboard.Count} concept node(s) to clipboard.";
    }

    [RelayCommand]
    public async Task PasteAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null || _clipboard.Count == 0) return;

        foreach (var n in CanvasNodes) n.IsSelected = false;

        var pastedVms = new List<CanvasNodeViewModel>();

        foreach (var clip in _clipboard)
        {
            var req = new CreateMindMapNodeRequest(
                Title: $"{clip.Title} (Copy)",
                Description: clip.Description,
                X: 400 + (pastedVms.Count * 30),
                Y: 200 + (pastedVms.Count * 30),
                Width: clip.Width,
                Height: clip.Height,
                ColorHex: clip.ColorHex,
                Shape: clip.Shape,
                NodeType: clip.NodeType,
                LinkedEntityType: clip.LinkedEntityType,
                LinkedEntityId: clip.LinkedEntityId
            );

            var res = await _apiClient.CreateMindMapNodeAsync(ws.Id, SelectedMindMap.Id, req);
            if (res.IsSuccess)
            {
                var n = res.Value;
                var vm = new CanvasNodeViewModel
                {
                    Id = n.Id,
                    MindMapId = n.MindMapId,
                    ParentNodeId = n.ParentNodeId,
                    Title = n.Title,
                    Description = n.Description,
                    X = n.X,
                    Y = n.Y,
                    Width = n.Width,
                    Height = n.Height,
                    ColorHex = n.ColorHex,
                    Shape = n.Shape,
                    NodeType = n.NodeType,
                    LinkedEntityType = n.LinkedEntityType,
                    LinkedEntityId = n.LinkedEntityId,
                    IsSelected = true
                };
                CanvasNodes.Add(vm);
                pastedVms.Add(vm);
            }
        }

        if (pastedVms.Count > 0)
        {
            SelectedNode = pastedVms.Last();
            StatusMessage = $"Pasted {pastedVms.Count} concept node(s).";
        }
    }

    public async Task ConnectNodesAsync(Guid sourceId, Guid targetId, string? label = null)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null) return;
        if (sourceId == targetId)
        {
            StatusMessage = "Cannot connect a node to itself.";
            return;
        }

        // Avoid duplicate edge in UI
        if (CanvasEdges.Any(e => e.SourceNodeId == sourceId && e.TargetNodeId == targetId))
        {
            StatusMessage = "A connection already exists between these nodes.";
            return;
        }

        var req = new CreateMindMapEdgeRequest(sourceId, targetId, label);
        var res = await _apiClient.CreateMindMapEdgeAsync(ws.Id, SelectedMindMap.Id, req);
        if (res.IsSuccess)
        {
            var e = res.Value;
            var src = CanvasNodes.FirstOrDefault(n => n.Id == sourceId);
            var tgt = CanvasNodes.FirstOrDefault(n => n.Id == targetId);
            if (src != null && tgt != null)
            {
                var edgeVm = new CanvasEdgeViewModel
                {
                    Id = e.Id,
                    MindMapId = e.MindMapId,
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    SourceX = src.CenterX,
                    SourceY = src.CenterY,
                    TargetX = tgt.CenterX,
                    TargetY = tgt.CenterY,
                    Label = e.Label,
                    RelationType = e.RelationType,
                    Style = e.Style,
                    EdgeType = e.EdgeType
                };
                CanvasEdges.Add(edgeVm);

                UndoRedo.PushAlreadyExecuted(new ConnectMindMapEdgeAction(
                    edgeVm,
                    edge => CanvasEdges.Add(edge),
                    edge => CanvasEdges.Remove(edge)
                ));
            }
            StatusMessage = "Connected nodes.";
        }
        else
        {
            StatusMessage = $"Connection failed: {res.Error.Description}";
        }
    }

    public void MoveNode(Guid nodeId, double newX, double newY, bool recordUndo = true)
    {
        var node = CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        var oldX = node.X;
        var oldY = node.Y;

        node.X = newX;
        node.Y = newY;

        // Update connected edges geometry immediately
        UpdateConnectedEdgesGeometry(nodeId);

        if (recordUndo)
        {
            UndoRedo.PushAlreadyExecuted(new MoveCanvasItemAction((x, y) =>
            {
                node.X = x;
                node.Y = y;
                UpdateConnectedEdgesGeometry(nodeId);
                QueueNodePersistence(node.Id);
            }, oldX, oldY, newX, newY));
        }

        QueueNodePersistence(nodeId);
    }

    public void MoveSelectedNodes(double deltaX, double deltaY, bool recordUndo = true)
    {
        var selected = SelectedNodes;
        if (selected.Count == 0 && SelectedNode != null)
        {
            selected = new[] { SelectedNode };
        }
        if (selected.Count == 0) return;

        var moveEntries = new List<(Action<double, double> SetPos, double OldX, double OldY, double NewX, double NewY)>();

        foreach (var node in selected)
        {
            var oldX = node.X;
            var oldY = node.Y;
            var newX = Math.Max(0, Math.Round(node.X + deltaX, 1));
            var newY = Math.Max(0, Math.Round(node.Y + deltaY, 1));

            node.X = newX;
            node.Y = newY;
            UpdateConnectedEdgesGeometry(node.Id);

            moveEntries.Add(((x, y) =>
            {
                node.X = x;
                node.Y = y;
                UpdateConnectedEdgesGeometry(node.Id);
                QueueNodePersistence(node.Id);
            }, oldX, oldY, newX, newY));

            QueueNodePersistence(node.Id);
        }

        if (recordUndo && moveEntries.Count > 0)
        {
            UndoRedo.PushAlreadyExecuted(new BatchMoveCanvasAction(moveEntries));
        }
    }

    public void ResizeNode(Guid nodeId, double newWidth, double newHeight, bool recordUndo = true)
    {
        var node = CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        var oldW = node.Width;
        var oldH = node.Height;

        node.Width = Math.Max(80, Math.Round(newWidth, 1));
        node.Height = Math.Max(40, Math.Round(newHeight, 1));
        UpdateConnectedEdgesGeometry(nodeId);

        if (recordUndo)
        {
            UndoRedo.PushAlreadyExecuted(new ResizeCanvasItemAction((w, h) =>
            {
                node.Width = w;
                node.Height = h;
                UpdateConnectedEdgesGeometry(node.Id);
                QueueNodePersistence(node.Id);
            }, oldW, oldH, node.Width, node.Height));
        }

        QueueNodePersistence(nodeId);
    }

    private void UpdateConnectedEdgesGeometry(Guid nodeId)
    {
        var node = CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        foreach (var edge in CanvasEdges)
        {
            if (edge.SourceNodeId == nodeId)
            {
                edge.SourceX = node.CenterX;
                edge.SourceY = node.CenterY;
            }
            else if (edge.TargetNodeId == nodeId)
            {
                edge.TargetX = node.CenterX;
                edge.TargetY = node.CenterY;
            }
        }
    }

    private void QueueNodePersistence(Guid nodeId)
    {
        _pendingPersistNodeIds.Add(nodeId);
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private CancellationTokenSource? _persistCts;

    private async void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (_pendingPersistNodeIds.Count == 0 || SelectedMindMap == null) return;

        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var ids = _pendingPersistNodeIds.ToList();
        _pendingPersistNodeIds.Clear();

        var updates = CanvasNodes
            .Where(n => ids.Contains(n.Id))
            .Select(n => new MindMapNodeBatchPositionDto(n.Id, n.X, n.Y, n.Width, n.Height))
            .ToList();

        if (updates.Count == 0) return;

        _persistCts?.Cancel();
        _persistCts = new CancellationTokenSource();
        var token = _persistCts.Token;

        try
        {
            var req = new BatchUpdateMindMapNodesRequest(updates);
            var res = await _apiClient.BatchUpdateMindMapNodesAsync(ws.Id, SelectedMindMap.Id, req, token);
            if (!res.IsSuccess && !token.IsCancellationRequested)
            {
                StatusMessage = $"Node sync failed: {res.Error.Description}";
            }
        }
        catch (OperationCanceledException)
        {
            // Newer request took precedence
        }
    }

    // ==================== Auto Layout ====================

    [RelayCommand]
    public async Task ApplyAutoLayoutAsync(string algorithmName)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null) return;

        var algo = AutoLayoutAlgorithm.Tree;
        if (Enum.TryParse<AutoLayoutAlgorithm>(algorithmName, true, out var parsed))
        {
            algo = parsed;
        }

        IsLoading = true;
        StatusMessage = $"Applying {algo} layout...";
        try
        {
            var res = await _apiClient.ApplyMindMapLayoutAsync(ws.Id, SelectedMindMap.Id, new ApplyLayoutRequest(algo), persist: true);
            if (res.IsSuccess)
            {
                var posMap = res.Value.Positions.ToDictionary(p => p.NodeId);
                foreach (var node in CanvasNodes)
                {
                    if (posMap.TryGetValue(node.Id, out var pos))
                    {
                        node.X = pos.X;
                        node.Y = pos.Y;
                    }
                }

                // Update all edges geometry
                var nodeMap = CanvasNodes.ToDictionary(n => n.Id);
                foreach (var edge in CanvasEdges)
                {
                    if (nodeMap.TryGetValue(edge.SourceNodeId, out var src) && nodeMap.TryGetValue(edge.TargetNodeId, out var tgt))
                    {
                        edge.SourceX = src.CenterX;
                        edge.SourceY = src.CenterY;
                        edge.TargetX = tgt.CenterX;
                        edge.TargetY = tgt.CenterY;
                    }
                }

                StatusMessage = $"Applied {algo} layout.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ==================== AI Actions ====================

    [RelayCommand]
    public async Task GenerateAiMindMapAsync(string? promptParam = null)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var prompt = !string.IsNullOrWhiteSpace(promptParam)
            ? promptParam.Trim()
            : (!string.IsNullOrWhiteSpace(AiPromptText) ? AiPromptText.Trim() : (SelectedMindMap != null ? SelectedMindMap.Title : "Core Concepts and Ideas"));
        AiPromptText = string.Empty;

        IsLoading = true;
        StatusMessage = "AI is generating Mind Map graph...";
        try
        {
            var res = await _apiClient.GenerateMindMapAsync(ws.Id, new GenerateMindMapRequest(prompt.Trim(), MaxNodes: 12));
            if (res.IsSuccess)
            {
                await LoadMindMapsAsync();
                var newMap = MindMaps.FirstOrDefault(m => m.Id == res.Value.MindMapId);
                if (newMap != null)
                {
                    await SelectMindMapAsync(newMap);
                }
                StatusMessage = $"AI generated '{res.Value.Title}' with {res.Value.NodeCount} concepts.";
            }
            else
            {
                StatusMessage = $"AI generation failed: {res.Error.Description}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI generation error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExplainSelectedNodeAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null || SelectedNode == null) return;

        IsLoading = true;
        StatusMessage = $"Explaining '{SelectedNode.Title}' with AI...";
        try
        {
            var res = await _apiClient.ExplainNodeAsync(ws.Id, SelectedMindMap.Id, SelectedNode.Id, new ExplainNodeRequest());
            if (res.IsSuccess)
            {
                AiPanelTitle = $"Explain: {res.Value.NodeTitle}";
                AiExplanationText = res.Value.Explanation;
                AiKeyTakeaways.Clear();
                foreach (var kt in res.Value.KeyTakeaways) AiKeyTakeaways.Add(kt);
                IsAiPanelOpen = true;
                StatusMessage = "AI explanation ready.";
            }
            else
            {
                StatusMessage = $"Error: {res.Error.Description}";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task FindRelatedKnowledgeAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedMindMap == null || SelectedNode == null) return;

        IsLoading = true;
        StatusMessage = $"Searching related knowledge for '{SelectedNode.Title}'...";
        try
        {
            var res = await _apiClient.FindRelatedKnowledgeAsync(ws.Id, SelectedMindMap.Id, SelectedNode.Id, new FindRelatedKnowledgeRequest(5));
            if (res.IsSuccess)
            {
                AiPanelTitle = $"Related Knowledge: {SelectedNode.Title}";
                RelatedKnowledgeItems.Clear();
                foreach (var item in res.Value.RelatedItems) RelatedKnowledgeItems.Add(item);
                IsAiPanelOpen = true;
                StatusMessage = $"Found {res.Value.RelatedItems.Count} related knowledge items.";
            }
            else
            {
                StatusMessage = $"Search failed: {res.Error.Description}";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void CloseAiPanel()
    {
        IsAiPanelOpen = false;
    }

    // ==================== Knowledge Navigation ====================

    [RelayCommand]
    public void NavigateToKnowledge(CanvasNodeViewModel? node)
    {
        if (node == null || !node.HasKnowledgeLink) return;

        var type = node.LinkedEntityType?.ToLowerInvariant();
        switch (type)
        {
            case "document":
                _navigationService.NavigateTo<DocumentsViewModel>();
                break;
            case "note":
                _navigationService.NavigateTo<NotesViewModel>();
                break;
            case "page":
                _navigationService.NavigateTo<PagesViewModel>();
                break;
            case "studytopic" or "quiz" or "flashcard":
                _navigationService.NavigateTo<StudyViewModel>();
                break;
        }
    }

    [RelayCommand]
    public void SwitchToBoards()
    {
        _navigationService.NavigateTo<BoardsViewModel>();
    }

    // ==================== Zoom & Pan ====================

    [RelayCommand]
    public void ZoomIn()
    {
        ZoomLevel = Math.Clamp(Math.Round(ZoomLevel + 0.1, 2), 0.1, 4.0);
        OnPropertyChanged(nameof(ZoomPercentageText));
    }

    [RelayCommand]
    public void ZoomOut()
    {
        ZoomLevel = Math.Clamp(Math.Round(ZoomLevel - 0.1, 2), 0.1, 4.0);
        OnPropertyChanged(nameof(ZoomPercentageText));
    }

    [RelayCommand]
    public void ResetZoom()
    {
        ZoomLevel = 1.0;
        PanX = 0;
        PanY = 0;
        OnPropertyChanged(nameof(ZoomPercentageText));
    }

    [RelayCommand]
    public void FitView()
    {
        if (CanvasNodes.Count == 0)
        {
            ResetZoom();
            return;
        }

        var minX = CanvasNodes.Min(n => n.X);
        var minY = CanvasNodes.Min(n => n.Y);

        PanX = Math.Max(20, -minX + 100);
        PanY = Math.Max(20, -minY + 80);
        ZoomLevel = 1.0;
        OnPropertyChanged(nameof(ZoomPercentageText));
    }

    [RelayCommand]
    public void Undo() => UndoRedo.Undo();

    [RelayCommand]
    public void Redo() => UndoRedo.Redo();

    [RelayCommand]
    public void SetTool(string toolName)
    {
        if (Enum.TryParse<CanvasTool>(toolName, true, out var tool))
        {
            ActiveTool = tool;
        }
    }
}
