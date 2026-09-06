using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexus.Desktop.ViewModels;

namespace Nexus.Desktop.Views;

public partial class BoardsView : UserControl
{
    private Point _lastMousePosition;
    private bool _isPanning;
    private bool _isDraggingItem;
    private CanvasItemViewModel? _draggedItem;
    private Point _itemDragStartPos;

    public BoardsView()
    {
        InitializeComponent();
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not BoardsViewModel vm) return;

        var delta = e.Delta > 0 ? 0.1 : -0.1;
        vm.ZoomLevel = Math.Clamp(Math.Round(vm.ZoomLevel + delta, 2), 0.1, 4.0);
        e.Handled = true;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BoardsViewModel vm) return;

        if (e.MiddleButton == MouseButtonState.Pressed || vm.ActiveTool == CanvasTool.Pan || Keyboard.IsKeyDown(Key.Space))
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(CanvasContainer);
            CanvasContainer.CaptureMouse();
            e.Handled = true;
        }
        else if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingItem)
        {
            // Deselect items when clicking blank canvas
            foreach (var item in vm.CanvasItems) item.IsSelected = false;
            vm.SelectedItem = null;
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not BoardsViewModel vm) return;

        if (_isPanning)
        {
            var currentPos = e.GetPosition(CanvasContainer);
            var deltaX = currentPos.X - _lastMousePosition.X;
            var deltaY = currentPos.Y - _lastMousePosition.Y;

            vm.PanX += deltaX;
            vm.PanY += deltaY;
            _lastMousePosition = currentPos;
            e.Handled = true;
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            CanvasContainer.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BoardsViewModel vm) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        if (sender is FrameworkElement element && element.DataContext is CanvasItemViewModel item)
        {
            _isDraggingItem = true;
            _draggedItem = item;
            _itemDragStartPos = e.GetPosition(VisualCanvas);

            foreach (var itm in vm.CanvasItems) itm.IsSelected = false;
            item.IsSelected = true;
            vm.SelectedItem = item;

            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not BoardsViewModel vm) return;

        if (_isDraggingItem && _draggedItem != null && sender is FrameworkElement element)
        {
            var currentPos = e.GetPosition(VisualCanvas);
            var deltaX = currentPos.X - _itemDragStartPos.X;
            var deltaY = currentPos.Y - _itemDragStartPos.Y;

            var newX = Math.Max(0, _draggedItem.X + deltaX);
            var newY = Math.Max(0, _draggedItem.Y + deltaY);

            vm.MoveItem(_draggedItem.Id, Math.Round(newX, 1), Math.Round(newY, 1));
            _itemDragStartPos = currentPos;
            e.Handled = true;
        }
    }

    private void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingItem && sender is FrameworkElement element)
        {
            _isDraggingItem = false;
            _draggedItem = null;
            element.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
