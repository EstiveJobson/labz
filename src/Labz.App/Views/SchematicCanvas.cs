using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Labz.App.ViewModels;
using Labz.Core.Drawing;

namespace Labz.App.Views;

public sealed class SchematicCanvas : FrameworkElement
{
    private SchematicViewModel? _viewModel;
    private bool _panning;
    private Point _last;

    public SchematicCanvas()
    {
        Focusable = true;
        SnapsToDevicePixels = true;
        DataContextChanged += (_, _) => Attach();
    }

    protected override void OnRender(DrawingContext context)
    {
        var viewModel = _viewModel;
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(244, 241, 234)), null, new Rect(RenderSize));
        if (viewModel is null)
        {
            return;
        }

        context.PushTransform(new TranslateTransform(viewModel.PanX, viewModel.PanY));
        context.PushTransform(new ScaleTransform(viewModel.Zoom, viewModel.Zoom));
        DrawGrid(context, viewModel);
        DrawScene(context, viewModel.Scene);
        context.Pop();
        context.Pop();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        _viewModel.ZoomAt(position.X, position.Y, e.Delta > 0 ? 1.1 : 1 / 1.1);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        if (_viewModel is null)
        {
            return;
        }

        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right)
        {
            _panning = true;
            _last = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var world = ToWorld(e.GetPosition(this));
        _viewModel.PointerDown(world.X, world.Y);
        CaptureMouse();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_panning)
        {
            _viewModel.PanBy(position.X - _last.X, position.Y - _last.Y);
            _last = position;
            InvalidateVisual();
            return;
        }

        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            var world = ToWorld(position);
            _viewModel.PointerMove(world.X, world.Y);
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_panning && e.ChangedButton is MouseButton.Middle or MouseButton.Right)
        {
            _panning = false;
            ReleaseMouseCapture();
            return;
        }

        if (_viewModel is not null && e.ChangedButton == MouseButton.Left)
        {
            var world = ToWorld(e.GetPosition(this));
            _viewModel.PointerUp(world.X, world.Y);
            ReleaseMouseCapture();
            InvalidateVisual();
        }
    }

    private void Attach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = DataContext as SchematicViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
        }

        InvalidateVisual();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private CanvasPoint ToWorld(Point screen)
    {
        var viewModel = _viewModel ?? throw new InvalidOperationException();
        return new CanvasPoint(
            (screen.X - viewModel.PanX) / viewModel.Zoom,
            (screen.Y - viewModel.PanY) / viewModel.Zoom);
    }

    private void DrawGrid(DrawingContext context, SchematicViewModel viewModel)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(226, 221, 212)), 1 / viewModel.Zoom);
        pen.Freeze();
        var left = (-viewModel.PanX) / viewModel.Zoom;
        var top = (-viewModel.PanY) / viewModel.Zoom;
        var right = left + (ActualWidth / viewModel.Zoom);
        var bottom = top + (ActualHeight / viewModel.Zoom);
        var startX = Math.Floor(left / GridSnap.Size) * GridSnap.Size;
        var startY = Math.Floor(top / GridSnap.Size) * GridSnap.Size;
        for (var x = startX; x <= right; x += GridSnap.Size)
        {
            context.DrawLine(pen, new Point(x, top), new Point(x, bottom));
        }

        for (var y = startY; y <= bottom; y += GridSnap.Size)
        {
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
        }
    }

    private static void DrawScene(DrawingContext context, CanvasScene scene)
    {
        var wirePen = Frozen(Color.FromRgb(36, 82, 122), 1.6);
        var bodyPen = Frozen(Color.FromRgb(31, 41, 51), 1.6);
        var selectedPen = Frozen(Color.FromRgb(29, 78, 137), 2.4);
        var problemPen = Frozen(Color.FromRgb(180, 35, 24), 2.4);
        var pinBrush = new SolidColorBrush(Color.FromRgb(194, 65, 12));
        pinBrush.Freeze();
        var textBrush = new SolidColorBrush(Color.FromRgb(31, 41, 51));
        textBrush.Freeze();

        foreach (var wire in scene.Wires)
        {
            context.DrawGeometry(null, wirePen, Polyline(wire.Points));
        }

        foreach (var symbol in scene.Symbols)
        {
            var pen = symbol.Highlighted ? problemPen : symbol.Selected ? selectedPen : bodyPen;
            foreach (var stroke in symbol.Strokes)
            {
                context.DrawGeometry(null, pen, Polyline(stroke.Points));
            }

            foreach (var pin in symbol.Pins)
            {
                context.DrawEllipse(pinBrush, null, new Point(pin.Position.X, pin.Position.Y), 3.5, 3.5);
            }
        }

        foreach (var label in scene.Labels)
        {
            var text = new FormattedText(
                label.Text,
                CultureInfo.GetCultureInfo("pt-BR"),
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                textBrush,
                1);
            context.DrawText(text, new Point(label.Position.X, label.Position.Y));
        }
    }

    private static Pen Frozen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static StreamGeometry Polyline(IReadOnlyList<CanvasPoint> points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        if (points.Count > 0)
        {
            context.BeginFigure(new Point(points[0].X, points[0].Y), false, false);
            if (points.Count > 1)
            {
                context.PolyLineTo(points.Skip(1).Select(point => new Point(point.X, point.Y)).ToArray(), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
