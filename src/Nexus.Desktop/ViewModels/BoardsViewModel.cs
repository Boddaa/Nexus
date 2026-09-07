using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Desktop.Models;
using Nexus.Desktop.Services;
using Nexus.Domain.Enums;

namespace Nexus.Desktop.ViewModels;

public partial class BoardsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly UserSession _userSession;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _debounceTimer;
    private readonly HashSet<Guid> _pendingPersistItemIds = new();

    [ObservableProperty]
    private ObservableCollection<BoardDto> _boards = new();

    [ObservableProperty]
    private BoardDto? _selectedBoard;

    [ObservableProperty]
    private ObservableCollection<CanvasItemViewModel> _canvasItems = new();

    [ObservableProperty]
    private CanvasItemViewModel? _selectedItem;

    [ObservableProperty]
    private CanvasTool _activeTool = CanvasTool.Select;

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

    public CanvasUndoRedoManager UndoRedo { get; } = new();

    public string ZoomPercentageText => $"{Math.Round(ZoomLevel * 100)}%";

    public BoardsViewModel(
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

        _ = LoadBoardsAsync();
    }

    [RelayCommand]
    public async Task LoadBoardsAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoading = true;
        StatusMessage = "Loading boards...";
        try
        {
            var res = await _apiClient.GetBoardsAsync(ws.Id);
            if (res.IsSuccess)
            {
                Boards.Clear();
                foreach (var b in res.Value) Boards.Add(b);

                if (Boards.Count > 0 && SelectedBoard == null)
                {
                    SelectedBoard = Boards[0];
                    await LoadBoardDetailsAsync(SelectedBoard.Id);
                }
                StatusMessage = $"{Boards.Count} board(s) loaded.";
            }
            else
            {
                StatusMessage = $"Error: {res.Error.Description}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load boards: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SelectBoardAsync(BoardDto? board)
    {
        if (board == null) return;
        SelectedBoard = board;
        await LoadBoardDetailsAsync(board.Id);
    }

    private async Task LoadBoardDetailsAsync(Guid boardId)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        IsLoading = true;
        try
        {
            var res = await _apiClient.GetBoardByIdAsync(ws.Id, boardId);
            if (res.IsSuccess)
            {
                CanvasItems.Clear();
                UndoRedo.Clear();
                foreach (var i in res.Value.Items)
                {
                    CanvasItems.Add(new CanvasItemViewModel
                    {
                        Id = i.Id,
                        BoardId = i.BoardId,
                        Type = i.Type,
                        Title = i.Title,
                        Description = i.Description,
                        Content = i.Content,
                        X = i.X,
                        Y = i.Y,
                        Width = i.Width,
                        Height = i.Height,
                        Rotation = i.Rotation,
                        ZIndex = i.ZIndex,
                        ColorHex = i.ColorHex,
                        LinkedEntityType = i.LinkedEntityType,
                        LinkedEntityId = i.LinkedEntityId
                    });
                }
                SelectedItem = CanvasItems.FirstOrDefault();
                StatusMessage = $"Board '{res.Value.Title}' loaded with {CanvasItems.Count} items.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [ObservableProperty]
    private string _newBoardTitle = string.Empty;

    [RelayCommand]
    public async Task CreateBoardAsync(string? titleParam = null)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var title = !string.IsNullOrWhiteSpace(titleParam)
            ? titleParam.Trim()
            : (!string.IsNullOrWhiteSpace(NewBoardTitle) ? NewBoardTitle.Trim() : $"Board {Boards.Count + 1}");
        NewBoardTitle = string.Empty;

        var res = await _apiClient.CreateBoardAsync(ws.Id, new CreateBoardRequest(title));
        if (res.IsSuccess)
        {
            Boards.Insert(0, res.Value);
            SelectedBoard = res.Value;
            CanvasItems.Clear();
            UndoRedo.Clear();
            StatusMessage = $"Created board '{res.Value.Title}'.";
        }
        else
        {
            StatusMessage = $"Error creating board: {res.Error.Description}";
        }
    }

    [RelayCommand]
    public async Task DeleteCurrentBoardAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedBoard == null) return;

        var confirm = await _dialogService.ConfirmAsync("Delete Board", $"Are you sure you want to delete '{SelectedBoard.Title}'?");
        if (!confirm) return;

        var res = await _apiClient.DeleteBoardAsync(ws.Id, SelectedBoard.Id);
        if (res.IsSuccess)
        {
            Boards.Remove(SelectedBoard);
            SelectedBoard = Boards.FirstOrDefault();
            if (SelectedBoard != null)
            {
                await LoadBoardDetailsAsync(SelectedBoard.Id);
            }
            else
            {
                CanvasItems.Clear();
            }
            StatusMessage = "Board deleted.";
        }
        else
        {
            StatusMessage = $"Delete failed: {res.Error.Description}";
        }
    }

    // ==================== Item Operations ====================

    [RelayCommand]
    public async Task AddItemAsync(string itemTypeStr)
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedBoard == null) return;

        var itemType = BoardItemType.StickyNote;
        if (Enum.TryParse<BoardItemType>(itemTypeStr, true, out var parsed))
        {
            itemType = parsed;
        }

        var title = itemType switch
        {
            BoardItemType.StickyNote => "New Note",
            BoardItemType.Text => "Text Block",
            BoardItemType.Shape => "Process Shape",
            BoardItemType.Document => "Document Reference",
            BoardItemType.Note => "Linked Note",
            BoardItemType.Page => "Linked Page",
            BoardItemType.Task => "Canvas Task",
            BoardItemType.MindMap => "Embedded MindMap",
            _ => "Visual Object"
        };

        var spawnX = 200 - PanX;
        var spawnY = 150 - PanY;

        var req = new CreateBoardItemRequest(
            Type: itemType,
            Title: title,
            X: Math.Max(20, spawnX),
            Y: Math.Max(20, spawnY),
            Width: itemType == BoardItemType.StickyNote ? 200 : 220,
            Height: itemType == BoardItemType.StickyNote ? 160 : 120,
            ColorHex: itemType == BoardItemType.StickyNote ? "#FEF08A" : "#1E293B"
        );

        var res = await _apiClient.CreateBoardItemAsync(ws.Id, SelectedBoard.Id, req);
        if (res.IsSuccess)
        {
            var i = res.Value;
            var vm = new CanvasItemViewModel
            {
                Id = i.Id,
                BoardId = i.BoardId,
                Type = i.Type,
                Title = i.Title,
                Description = i.Description,
                Content = i.Content,
                X = i.X,
                Y = i.Y,
                Width = i.Width,
                Height = i.Height,
                Rotation = i.Rotation,
                ZIndex = i.ZIndex,
                ColorHex = i.ColorHex,
                LinkedEntityType = i.LinkedEntityType,
                LinkedEntityId = i.LinkedEntityId,
                IsSelected = true
            };

            foreach (var item in CanvasItems) item.IsSelected = false;
            CanvasItems.Add(vm);
            SelectedItem = vm;
            StatusMessage = $"Added {itemType}.";
        }
    }

    public IReadOnlyList<CanvasItemViewModel> SelectedItems => CanvasItems.Where(i => i.IsSelected).ToList();

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in CanvasItems) item.IsSelected = true;
        if (SelectedItem == null) SelectedItem = CanvasItems.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedItems));
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var item in CanvasItems) item.IsSelected = false;
        SelectedItem = null;
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void ToggleItemSelection(CanvasItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;
        if (item.IsSelected && SelectedItem == null) SelectedItem = item;
        else if (!item.IsSelected && SelectedItem == item) SelectedItem = CanvasItems.FirstOrDefault(i => i.IsSelected);
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void SetSingleSelection(CanvasItemViewModel item)
    {
        foreach (var itm in CanvasItems) itm.IsSelected = (itm.Id == item.Id);
        SelectedItem = item;
        OnPropertyChanged(nameof(SelectedItems));
    }

    [RelayCommand]
    public async Task DeleteSelectedItemAsync()
    {
        await DeleteSelectedAsync();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedBoard == null) return;

        var selected = SelectedItems;
        if (selected.Count == 0 && SelectedItem != null)
        {
            selected = new[] { SelectedItem };
        }
        if (selected.Count == 0) return;

        int deletedCount = 0;
        var deletedItems = new List<CanvasItemViewModel>();
        foreach (var item in selected.ToList())
        {
            var res = await _apiClient.DeleteBoardItemAsync(ws.Id, SelectedBoard.Id, item.Id);
            if (res.IsSuccess)
            {
                CanvasItems.Remove(item);
                deletedItems.Add(item);
                deletedCount++;
            }
        }

        if (deletedItems.Count > 0)
        {
            UndoRedo.PushAlreadyExecuted(new DeleteCanvasItemAction<IReadOnlyList<CanvasItemViewModel>>(
                deletedItems,
                items => { foreach (var i in items) if (!CanvasItems.Contains(i)) CanvasItems.Add(i); },
                items => { foreach (var i in items) CanvasItems.Remove(i); }
            ));
        }

        SelectedItem = CanvasItems.LastOrDefault();
        StatusMessage = $"Deleted {deletedCount} item(s).";
    }

    private static readonly List<ClipboardItemData> _clipboard = new();

    [RelayCommand]
    public void CopySelected()
    {
        var selected = SelectedItems;
        if (selected.Count == 0 && SelectedItem != null)
        {
            selected = new[] { SelectedItem };
        }
        if (selected.Count == 0) return;

        _clipboard.Clear();
        foreach (var item in selected)
        {
            _clipboard.Add(new ClipboardItemData(
                item.Type,
                item.Title,
                item.Description,
                item.Content,
                item.Width,
                item.Height,
                item.Rotation,
                item.ColorHex,
                item.LinkedEntityType,
                item.LinkedEntityId
            ));
        }
        StatusMessage = $"Copied {_clipboard.Count} item(s) to clipboard.";
    }

    [RelayCommand]
    public async Task PasteAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedBoard == null || _clipboard.Count == 0) return;

        foreach (var itm in CanvasItems) itm.IsSelected = false;

        var pastedVms = new List<CanvasItemViewModel>();

        foreach (var clip in _clipboard)
        {
            var req = new CreateBoardItemRequest(
                Type: clip.Type,
                Title: $"{clip.Title} (Copy)",
                Description: clip.Description,
                Content: clip.Content,
                X: 200 + (pastedVms.Count * 30),
                Y: 150 + (pastedVms.Count * 30),
                Width: clip.Width,
                Height: clip.Height,
                Rotation: clip.Rotation,
                ColorHex: clip.ColorHex,
                LinkedEntityType: clip.LinkedEntityType,
                LinkedEntityId: clip.LinkedEntityId
            );

            var res = await _apiClient.CreateBoardItemAsync(ws.Id, SelectedBoard.Id, req);
            if (res.IsSuccess)
            {
                var i = res.Value;
                var vm = new CanvasItemViewModel
                {
                    Id = i.Id,
                    BoardId = i.BoardId,
                    Type = i.Type,
                    Title = i.Title,
                    Description = i.Description,
                    Content = i.Content,
                    X = i.X,
                    Y = i.Y,
                    Width = i.Width,
                    Height = i.Height,
                    Rotation = i.Rotation,
                    ZIndex = i.ZIndex,
                    ColorHex = i.ColorHex,
                    LinkedEntityType = i.LinkedEntityType,
                    LinkedEntityId = i.LinkedEntityId,
                    IsSelected = true
                };
                CanvasItems.Add(vm);
                pastedVms.Add(vm);
            }
        }

        if (pastedVms.Count > 0)
        {
            SelectedItem = pastedVms.Last();
            StatusMessage = $"Pasted {pastedVms.Count} item(s).";
        }
    }

    [RelayCommand]
    public async Task DuplicateSelectedItemAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedBoard == null || SelectedItem == null) return;

        var req = new CreateBoardItemRequest(
            Type: SelectedItem.Type,
            Title: $"{SelectedItem.Title} (Copy)",
            Description: SelectedItem.Description,
            Content: SelectedItem.Content,
            X: SelectedItem.X + 30,
            Y: SelectedItem.Y + 30,
            Width: SelectedItem.Width,
            Height: SelectedItem.Height,
            Rotation: SelectedItem.Rotation,
            ZIndex: SelectedItem.ZIndex + 1,
            ColorHex: SelectedItem.ColorHex,
            LinkedEntityType: SelectedItem.LinkedEntityType,
            LinkedEntityId: SelectedItem.LinkedEntityId
        );

        var res = await _apiClient.CreateBoardItemAsync(ws.Id, SelectedBoard.Id, req);
        if (res.IsSuccess)
        {
            var i = res.Value;
            var vm = new CanvasItemViewModel
            {
                Id = i.Id,
                BoardId = i.BoardId,
                Type = i.Type,
                Title = i.Title,
                Description = i.Description,
                Content = i.Content,
                X = i.X,
                Y = i.Y,
                Width = i.Width,
                Height = i.Height,
                Rotation = i.Rotation,
                ZIndex = i.ZIndex,
                ColorHex = i.ColorHex,
                LinkedEntityType = i.LinkedEntityType,
                LinkedEntityId = i.LinkedEntityId,
                IsSelected = true
            };
            foreach (var itm in CanvasItems) itm.IsSelected = false;
            CanvasItems.Add(vm);
            SelectedItem = vm;
            StatusMessage = "Duplicated item.";
        }
    }

    public void MoveItem(Guid itemId, double newX, double newY, bool recordUndo = true)
    {
        var item = CanvasItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return;

        var oldX = item.X;
        var oldY = item.Y;

        item.X = newX;
        item.Y = newY;

        if (recordUndo)
        {
            UndoRedo.PushAlreadyExecuted(new MoveCanvasItemAction((x, y) =>
            {
                item.X = x;
                item.Y = y;
                QueueItemPersistence(item.Id);
            }, oldX, oldY, newX, newY));
        }

        QueueItemPersistence(itemId);
    }

    public void MoveSelectedItems(double deltaX, double deltaY, bool recordUndo = true)
    {
        var selected = SelectedItems;
        if (selected.Count == 0 && SelectedItem != null)
        {
            selected = new[] { SelectedItem };
        }
        if (selected.Count == 0) return;

        var moveEntries = new List<(Action<double, double> SetPos, double OldX, double OldY, double NewX, double NewY)>();

        foreach (var item in selected)
        {
            var oldX = item.X;
            var oldY = item.Y;
            var newX = Math.Max(0, Math.Round(item.X + deltaX, 1));
            var newY = Math.Max(0, Math.Round(item.Y + deltaY, 1));

            item.X = newX;
            item.Y = newY;

            moveEntries.Add(((x, y) =>
            {
                item.X = x;
                item.Y = y;
                QueueItemPersistence(item.Id);
            }, oldX, oldY, newX, newY));

            QueueItemPersistence(item.Id);
        }

        if (recordUndo && moveEntries.Count > 0)
        {
            UndoRedo.PushAlreadyExecuted(new BatchMoveCanvasAction(moveEntries));
        }
    }

    public void ResizeItem(Guid itemId, double newWidth, double newHeight, bool recordUndo = true)
    {
        var item = CanvasItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return;

        var oldW = item.Width;
        var oldH = item.Height;

        item.Width = Math.Max(50, Math.Round(newWidth, 1));
        item.Height = Math.Max(40, Math.Round(newHeight, 1));

        if (recordUndo)
        {
            UndoRedo.PushAlreadyExecuted(new ResizeCanvasItemAction((w, h) =>
            {
                item.Width = w;
                item.Height = h;
                QueueItemPersistence(item.Id);
            }, oldW, oldH, item.Width, item.Height));
        }

        QueueItemPersistence(itemId);
    }

    private void QueueItemPersistence(Guid itemId)
    {
        _pendingPersistItemIds.Add(itemId);
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private CancellationTokenSource? _persistCts;

    private async void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (_pendingPersistItemIds.Count == 0 || SelectedBoard == null) return;

        var ws = _userSession.SelectedWorkspace;
        if (ws == null) return;

        var ids = _pendingPersistItemIds.ToList();
        _pendingPersistItemIds.Clear();

        var updates = CanvasItems
            .Where(i => ids.Contains(i.Id))
            .Select(i => new BoardItemBatchPositionDto(i.Id, i.X, i.Y, i.Width, i.Height, i.Rotation, i.ZIndex))
            .ToList();

        if (updates.Count == 0) return;

        _persistCts?.Cancel();
        _persistCts = new CancellationTokenSource();
        var token = _persistCts.Token;

        try
        {
            var req = new BatchUpdateBoardItemsRequest(updates);
            var res = await _apiClient.BatchUpdateBoardItemsAsync(ws.Id, SelectedBoard.Id, req, token);
            if (!res.IsSuccess && !token.IsCancellationRequested)
            {
                StatusMessage = $"Sync failed: {res.Error.Description}";
            }
        }
        catch (OperationCanceledException)
        {
            // Newer request took precedence
        }
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
        if (CanvasItems.Count == 0)
        {
            ResetZoom();
            return;
        }

        var minX = CanvasItems.Min(i => i.X);
        var minY = CanvasItems.Min(i => i.Y);
        var maxX = CanvasItems.Max(i => i.X + i.Width);
        var maxY = CanvasItems.Max(i => i.Y + i.Height);

        PanX = Math.Max(20, -minX + 50);
        PanY = Math.Max(20, -minY + 50);
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

    // ==================== Knowledge Navigation ====================

    [RelayCommand]
    public void NavigateToKnowledge(CanvasItemViewModel? item)
    {
        if (item == null || !item.HasKnowledgeLink) return;

        var type = item.LinkedEntityType?.ToLowerInvariant();
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
            case "mindmap":
                _navigationService.NavigateTo<MindMapsViewModel>();
                break;
        }
    }

    [RelayCommand]
    public void SwitchToMindMaps()
    {
        _navigationService.NavigateTo<MindMapsViewModel>();
    }
}
