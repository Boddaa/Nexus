using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Domain.Enums;

namespace Nexus.Desktop.ViewModels;

public enum CanvasTool
{
    Select,
    Pan,
    StickyNote,
    Text,
    Shape,
    Connect
}

public interface IUndoableAction
{
    string Description { get; }
    void Undo();
    void Redo();
}

public class CanvasUndoRedoManager : ObservableObject
{
    private const int MaxHistory = 100;
    private readonly List<IUndoableAction> _undoStack = new();
    private readonly List<IUndoableAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public void Execute(IUndoableAction action)
    {
        action.Redo();
        _undoStack.Add(action);
        if (_undoStack.Count > MaxHistory)
        {
            _undoStack.RemoveAt(0);
        }
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
    }

    public void PushAlreadyExecuted(IUndoableAction action)
    {
        _undoStack.Add(action);
        if (_undoStack.Count > MaxHistory)
        {
            _undoStack.RemoveAt(0);
        }
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        action.Undo();
        _redoStack.Add(action);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        action.Redo();
        _undoStack.Add(action);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
    }
}

public class MoveCanvasItemAction : IUndoableAction
{
    private readonly Action<double, double> _setPosition;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public string Description => "Move Item";

    public MoveCanvasItemAction(Action<double, double> setPosition, double oldX, double oldY, double newX, double newY)
    {
        _setPosition = setPosition;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public void Undo() => _setPosition(_oldX, _oldY);
    public void Redo() => _setPosition(_newX, _newY);
}

public class ResizeCanvasItemAction : IUndoableAction
{
    private readonly Action<double, double> _setSize;
    private readonly double _oldW, _oldH;
    private readonly double _newW, _newH;

    public string Description => "Resize Item";

    public ResizeCanvasItemAction(Action<double, double> setSize, double oldW, double oldH, double newW, double newH)
    {
        _setSize = setSize;
        _oldW = oldW;
        _oldH = oldH;
        _newW = newW;
        _newH = newH;
    }

    public void Undo() => _setSize(_oldW, _oldH);
    public void Redo() => _setSize(_newW, _newH);
}

public class BatchMoveCanvasAction : IUndoableAction
{
    private readonly List<(Action<double, double> SetPos, double OldX, double OldY, double NewX, double NewY)> _items;
    public string Description => $"Move {_items.Count} Items";

    public BatchMoveCanvasAction(List<(Action<double, double> SetPos, double OldX, double OldY, double NewX, double NewY)> items)
    {
        _items = items;
    }

    public void Undo()
    {
        foreach (var item in _items)
        {
            item.SetPos(item.OldX, item.OldY);
        }
    }

    public void Redo()
    {
        foreach (var item in _items)
        {
            item.SetPos(item.NewX, item.NewY);
        }
    }
}

public class CreateCanvasItemAction<T> : IUndoableAction
{
    private readonly Action<T> _add;
    private readonly Action<T> _remove;
    private readonly T _item;
    public string Description => "Create Item";

    public CreateCanvasItemAction(T item, Action<T> add, Action<T> remove)
    {
        _item = item;
        _add = add;
        _remove = remove;
    }

    public void Undo() => _remove(_item);
    public void Redo() => _add(_item);
}

public class DeleteCanvasItemAction<T> : IUndoableAction
{
    private readonly Action<T> _add;
    private readonly Action<T> _remove;
    private readonly T _item;
    public string Description => "Delete Item";

    public DeleteCanvasItemAction(T item, Action<T> add, Action<T> remove)
    {
        _item = item;
        _add = add;
        _remove = remove;
    }

    public void Undo() => _add(_item);
    public void Redo() => _remove(_item);
}

public class ConnectMindMapEdgeAction : IUndoableAction
{
    private readonly CanvasEdgeViewModel _edge;
    private readonly Action<CanvasEdgeViewModel> _add;
    private readonly Action<CanvasEdgeViewModel> _remove;
    public string Description => "Connect Nodes";

    public ConnectMindMapEdgeAction(CanvasEdgeViewModel edge, Action<CanvasEdgeViewModel> add, Action<CanvasEdgeViewModel> remove)
    {
        _edge = edge;
        _add = add;
        _remove = remove;
    }

    public void Undo() => _remove(_edge);
    public void Redo() => _add(_edge);
}

public class TextEditCanvasItemAction : IUndoableAction
{
    private readonly Action<string> _setText;
    private readonly string _oldText;
    private readonly string _newText;
    public string Description => "Edit Text";

    public TextEditCanvasItemAction(Action<string> setText, string oldText, string newText)
    {
        _setText = setText;
        _oldText = oldText;
        _newText = newText;
    }

    public void Undo() => _setText(_oldText);
    public void Redo() => _setText(_newText);
}

public record ClipboardItemData(
    BoardItemType Type,
    string Title,
    string? Description,
    string? Content,
    double Width,
    double Height,
    double Rotation,
    string? ColorHex,
    string? LinkedEntityType,
    Guid? LinkedEntityId
);

public record ClipboardNodeData(
    string Title,
    string? Description,
    double Width,
    double Height,
    string ColorHex,
    string Shape,
    MindMapNodeType NodeType,
    string? LinkedEntityType,
    Guid? LinkedEntityId
);

public partial class CanvasItemViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public Guid BoardId { get; init; }

    [ObservableProperty]
    private BoardItemType _type;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _content;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 200;

    [ObservableProperty]
    private double _height = 150;

    [ObservableProperty]
    private double _rotation;

    [ObservableProperty]
    private int _zIndex;

    [ObservableProperty]
    private string? _colorHex = "#262626";

    [ObservableProperty]
    private string? _linkedEntityType;

    [ObservableProperty]
    private Guid? _linkedEntityId;

    [ObservableProperty]
    private bool _isSelected;

    public bool HasKnowledgeLink => !string.IsNullOrWhiteSpace(LinkedEntityType) && LinkedEntityId.HasValue;

    public string Icon => Type switch
    {
        BoardItemType.StickyNote => "📌",
        BoardItemType.Text => "📝",
        BoardItemType.Shape => "🔷",
        BoardItemType.Document => "📄",
        BoardItemType.Note => "📓",
        BoardItemType.Page => "📑",
        BoardItemType.Task => "✅",
        BoardItemType.MindMap => "🧠",
        BoardItemType.ImagePlaceholder => "🖼️",
        _ => "📦"
    };

    public string BorderBrushHex => IsSelected ? "#06B6D4" : "#374151";
}

public partial class CanvasNodeViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public Guid MindMapId { get; init; }

    [ObservableProperty]
    private Guid? _parentNodeId;

    [ObservableProperty]
    private string _title = "Concept";

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 180;

    [ObservableProperty]
    private double _height = 80;

    [ObservableProperty]
    private string _colorHex = "#3B82F6";

    [ObservableProperty]
    private string _shape = "RoundedRectangle";

    [ObservableProperty]
    private MindMapNodeType _nodeType = MindMapNodeType.Concept;

    [ObservableProperty]
    private string? _linkedEntityType;

    [ObservableProperty]
    private Guid? _linkedEntityId;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRoot;

    public bool HasKnowledgeLink => !string.IsNullOrWhiteSpace(LinkedEntityType) && LinkedEntityId.HasValue;

    public double CenterX => X + (Width / 2.0);
    public double CenterY => Y + (Height / 2.0);

    public string BorderBrushHex => IsSelected ? "#06B6D4" : (IsRoot ? "#8B5CF6" : "#4B5563");
}

public partial class CanvasEdgeViewModel : ObservableObject
{
    public Guid Id { get; init; }
    public Guid MindMapId { get; init; }
    public Guid SourceNodeId { get; init; }
    public Guid TargetNodeId { get; init; }

    [ObservableProperty]
    private double _sourceX;

    [ObservableProperty]
    private double _sourceY;

    [ObservableProperty]
    private double _targetX;

    [ObservableProperty]
    private double _targetY;

    [ObservableProperty]
    private string? _label;

    [ObservableProperty]
    private string? _relationType;

    [ObservableProperty]
    private string _style = "Solid";

    [ObservableProperty]
    private MindMapEdgeType _edgeType = MindMapEdgeType.RelatesTo;

    [ObservableProperty]
    private bool _isSelected;

    public double LabelX => (SourceX + TargetX) / 2.0;
    public double LabelY => (SourceY + TargetY) / 2.0;

    public string StrokeBrushHex => IsSelected ? "#06B6D4" : "#6B7280";
}
