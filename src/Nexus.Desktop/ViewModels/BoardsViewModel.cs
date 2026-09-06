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

    [RelayCommand]
    public async Task DeleteSelectedItemAsync()
    {
        var ws = _userSession.SelectedWorkspace;
        if (ws == null || SelectedBoard == null || SelectedItem == null) return;

        var item = SelectedItem;
        var res = await _apiClient.DeleteBoardItemAsync(ws.Id, SelectedBoard.Id, item.Id);
        if (res.IsSuccess)
        {
            CanvasItems.Remove(item);
            SelectedItem = CanvasItems.LastOrDefault();
            StatusMessage = "Item deleted.";
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

    public void ResizeItem(Guid itemId, double newWidth, double newHeight, bool recordUndo = true)
    {
        var item = CanvasItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return;

        var oldW = item.Width;
        var oldH = item.Height;

        item.Width = Math.Max(50, newWidth);
        item.Height = Math.Max(40, newHeight);

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

        var req = new BatchUpdateBoardItemsRequest(updates);
        var res = await _apiClient.BatchUpdateBoardItemsAsync(ws.Id, SelectedBoard.Id, req);
        if (!res.IsSuccess)
        {
            StatusMessage = $"Sync failed: {res.Error.Description}";
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
