using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ortho.Application.Imaging;
using SkiaSharp;

namespace Ortho.UI.Controls;

/// <summary>
/// Superposition T0/T1 : radiographie T0 en fond, tracé T0 (cyan) et tracé T1
/// recalé (orange) par-dessus. Zoom molette, pan à la souris.
/// </summary>
public class TracingOverlayControl : Control
{
    private SKBitmap? _bitmap;
    private IReadOnlyList<(ImagePoint A, ImagePoint B)> _referenceSegments = [];
    private IReadOnlyList<(ImagePoint A, ImagePoint B)> _overlaySegments = [];

    private double _zoom = 1;
    private Point _pan;
    private bool _fitted;
    private bool _panning;
    private Point _lastPointer;

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(16, 16, 16));
    private static readonly Pen ReferencePen = new(Brushes.Cyan, 1.5);
    private static readonly Pen OverlayPen = new(Brushes.Orange, 1.5);

    public TracingOverlayControl()
    {
        ClipToBounds = true;
    }

    public void SetData(
        SKBitmap? bitmap,
        IReadOnlyList<(ImagePoint A, ImagePoint B)> referenceSegments,
        IReadOnlyList<(ImagePoint A, ImagePoint B)> overlaySegments)
    {
        _bitmap = bitmap;
        _referenceSegments = referenceSegments;
        _overlaySegments = overlaySegments;
        _fitted = false;
        InvalidateVisual();
    }

    private Matrix ViewMatrix => _bitmap is null
        ? Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_pan.X, _pan.Y)
        : Matrix.CreateTranslation(-_bitmap.Width / 2.0, -_bitmap.Height / 2.0)
          * Matrix.CreateScale(_zoom, _zoom)
          * Matrix.CreateTranslation(Bounds.Width / 2 + _pan.X, Bounds.Height / 2 + _pan.Y);

    private Point ImageToScreen(ImagePoint p) => ViewMatrix.Transform(new Point(p.X, p.Y));

    private void EnsureFitted()
    {
        if (_fitted || _bitmap is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        _zoom = Math.Min(Bounds.Width / _bitmap.Width, Bounds.Height / _bitmap.Height) * 0.95;
        _pan = default;
        _fitted = true;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(BackgroundBrush, new Rect(Bounds.Size));
        EnsureFitted();

        if (_bitmap is not null)
            context.Custom(new SkiaImageDrawOperation(
                new Rect(Bounds.Size), _bitmap, ViewMatrix, 0, 0));

        foreach (var (a, b) in _referenceSegments)
            context.DrawLine(ReferencePen, ImageToScreen(a), ImageToScreen(b));
        foreach (var (a, b) in _overlaySegments)
            context.DrawLine(OverlayPen, ImageToScreen(a), ImageToScreen(b));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pos = e.GetPosition(this);
        if (!ViewMatrix.TryInvert(out var inverse))
            return;
        var anchor = inverse.Transform(pos);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.15, e.Delta.Y), 0.02, 50);
        var newScreen = ViewMatrix.Transform(anchor);
        _pan = new Point(_pan.X + pos.X - newScreen.X, _pan.Y + pos.Y - newScreen.Y);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _panning = true;
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_panning)
            return;
        var pos = e.GetPosition(this);
        _pan = new Point(_pan.X + pos.X - _lastPointer.X, _pan.Y + pos.Y - _lastPointer.Y);
        _lastPointer = pos;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _panning = false;
        e.Pointer.Capture(null);
    }
}
