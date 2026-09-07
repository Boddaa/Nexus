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

    private bool _isResizingNode;
    private CanvasNodeViewModel? _resizingNode;
    private Point _nodeResizeStartPos;
    private double _nodeStartWidth;
    private double _nodeStartHeight;

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

            var isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (isCtrl)
            {
                vm.ToggleNodeSelection(node);
            }
            else if (!node.IsSelected)
            {
                vm.SetSingleSelection(node);
            }

            _isDraggingNode = true;
            _draggedNode = node;
            _nodeDragStartPos = e.GetPosition(VisualCanvas);

            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnNodeMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;

        if (_isDraggingNode && _draggedNode != null && sender is FrameworkElement)
        {
            var currentPos = e.GetPosition(VisualCanvas);
            var deltaX = currentPos.X - _nodeDragStartPos.X;
            var deltaY = currentPos.Y - _nodeDragStartPos.Y;

            if (Math.Abs(deltaX) > 0.01 || Math.Abs(deltaY) > 0.01)
            {
                vm.MoveSelectedNodes(deltaX, deltaY);
                _nodeDragStartPos = currentPos;
            }
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

    private void OnNodeResizeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MindMapsViewModel) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        if (sender is FrameworkElement element && element.DataContext is CanvasNodeViewModel node)
        {
            _isResizingNode = true;
            _resizingNode = node;
            _nodeResizeStartPos = e.GetPosition(VisualCanvas);
            _nodeStartWidth = node.Width;
            _nodeStartHeight = node.Height;

            element.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnNodeResizeMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not MindMapsViewModel vm) return;

        if (_isResizingNode && _resizingNode != null && sender is FrameworkElement)
        {
            var currentPos = e.GetPosition(VisualCanvas);
            var deltaX = currentPos.X - _nodeResizeStartPos.X;
            var deltaY = currentPos.Y - _nodeResizeStartPos.Y;

            var newW = Math.Max(80, _nodeStartWidth + deltaX);
            var newH = Math.Max(40, _nodeStartHeight + deltaY);

            vm.ResizeNode(_resizingNode.Id, newW, newH);
            e.Handled = true;
        }
    }

    private void OnNodeResizeMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingNode && sender is FrameworkElement element)
        {
            _isResizingNode = false;
            _resizingNode = null;
            element.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
