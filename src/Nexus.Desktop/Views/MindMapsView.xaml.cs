using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexus.Desktop.ViewModels;

namespace Nexus.Desktop.Views;

public partial class MindMapsView : UserControl
{
    private Point _lastMousePosition;
    private bool _isPanning;
    private bool _isDraggingNode;
    private CanvasNodeViewModel? _draggedNode;
    private Point _nodeDragStartPos;

    public MindMapsView()
    {
        InitializeComponent();
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;

        var delta = e.Delta > 0 ? 0.1 : -0.1;
        vm.ZoomLevel = Math.Clamp(Math.Round(vm.ZoomLevel + delta, 2), 0.1, 4.0);
        e.Handled = true;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;

        if (e.MiddleButton == MouseButtonState.Pressed || vm.ActiveTool == CanvasTool.Pan || Keyboard.IsKeyDown(Key.Space))
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(CanvasContainer);
            CanvasContainer.CaptureMouse();
            e.Handled = true;
        }
        else if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingNode)
        {
            foreach (var n in vm.CanvasNodes) n.IsSelected = false;
            foreach (var edge in vm.CanvasEdges) edge.IsSelected = false;
            vm.SelectedNode = null;
            vm.SelectedEdge = null;
            vm.ConnectingSourceNodeId = null;
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;

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

    private async void OnNodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        if (sender is FrameworkElement element && element.DataContext is CanvasNodeViewModel node)
        {
            if (vm.ActiveTool == CanvasTool.Connect)
            {
                if (vm.ConnectingSourceNodeId == null)
                {
                    vm.ConnectingSourceNodeId = node.Id;
                    node.IsSelected = true;
                    vm.StatusMessage = $"Connecting from '{node.Title}'... Click target concept node.";
                }
                else if (vm.ConnectingSourceNodeId != node.Id)
                {
                    var sourceId = vm.ConnectingSourceNodeId.Value;
                    vm.ConnectingSourceNodeId = null;
                    await vm.ConnectNodesAsync(sourceId, node.Id, "relates to");
                }
                e.Handled = true;
                return;
            }

            _isDraggingNode = true;
            _draggedNode = node;
            _nodeDragStartPos = e.GetPosition(VisualCanvas);

            foreach (var n in vm.CanvasNodes) n.IsSelected = false;
            node.IsSelected = true;
            vm.SelectedNode = node;

            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnNodeMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;

        if (_isDraggingNode && _draggedNode != null && sender is FrameworkElement element)
        {
            var currentPos = e.GetPosition(VisualCanvas);
            var deltaX = currentPos.X - _nodeDragStartPos.X;
            var deltaY = currentPos.Y - _nodeDragStartPos.Y;

            var newX = Math.Max(0, _draggedNode.X + deltaX);
            var newY = Math.Max(0, _draggedNode.Y + deltaY);

            vm.MoveNode(_draggedNode.Id, Math.Round(newX, 1), Math.Round(newY, 1));
            _nodeDragStartPos = currentPos;
            e.Handled = true;
        }
    }

    private void OnNodeMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingNode && sender is FrameworkElement element)
        {
            _isDraggingNode = false;
            _draggedNode = null;
            element.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
