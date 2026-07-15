using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ortho.Application.Imaging;
using SkiaSharp;

namespace Ortho.UI.Controls;

public record PlacedLandmark(string Code, ImagePoint Point, bool IsCurrent);

/// <summary>
/// Canvas de céphalométrie : image radiographique + landmarks + tracé.
/// Clic gauche : place le point courant (ou déplace un point existant saisi).
/// Clic droit / molette pressée : pan. Molette : zoom. Loupe de précision au survol.
/// </summary>
public class CephCanvasControl : Control
{
    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<CephCanvasControl, double>(nameof(Brightness));

    public static readonly StyledProperty<double> ContrastProperty =
        AvaloniaProperty.Register<CephCanvasControl, double>(nameof(Contrast));

    static CephCanvasControl()
    {
        AffectsRender<CephCanvasControl>(BrightnessProperty, ContrastProperty);
    }

    public double Brightness
    {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    public double Contrast
    {
        get => GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    /// <summary>Clic de placement du landmark courant.</summary>
    public event Action<ImagePoint>? LandmarkPlaced;

    /// <summary>Landmark existant déplacé par glisser-déposer.</summary>
    public event Action<string, ImagePoint>? LandmarkMoved;

    private const double MagnifierSize = 160;
    private const double MagnifierFactor = 3;
    private const double GrabRadius = 10;

    private SKBitmap? _bitmap;
    private IReadOnlyList<PlacedLandmark> _landmarks = [];
    private IReadOnlyList<(ImagePoint A, ImagePoint B)> _traceLines = [];
    private bool _placementActive;

    private double _zoom = 1;
    private Point _pan;
    private bool _fitted;

    private bool _panning;
    private Point _lastPointer;
    private string? _draggingCode;
    private Point? _hoverScreen;

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(16, 16, 16));
    private static readonly Pen TracePen = new(new SolidColorBrush(Color.FromArgb(170, 0, 220, 220)), 1);
    private static readonly IBrush PlacedBrush = Brushes.Cyan;
    private static readonly IBrush CurrentBrush = Brushes.Orange;
    private static readonly Pen CrosshairPen = new(Brushes.Orange, 1);
    private static readonly Pen MagnifierBorderPen = new(Brushes.White, 1.5);

    public CephCanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public void SetImage(SKBitmap bitmap)
    {
        _bitmap = bitmap;
        _fitted = false;
        InvalidateVisual();
    }

    public void SetState(
        IReadOnlyList<PlacedLandmark> landmarks,
        IReadOnlyList<(ImagePoint A, ImagePoint B)> traceLines,
        bool placementActive)
    {
        _landmarks = landmarks;
        _traceLines = traceLines;
        _placementActive = placementActive;
        InvalidateVisual();
    }

    private Matrix ViewMatrix => _bitmap is null
        ? Matrix.Identity
        : Matrix.CreateTranslation(-_bitmap.Width / 2.0, -_bitmap.Height / 2.0)
          * Matrix.CreateScale(_zoom, _zoom)
          * Matrix.CreateTranslation(Bounds.Width / 2 + _pan.X, Bounds.Height / 2 + _pan.Y);

    private Point ImageToScreen(ImagePoint p) => ViewMatrix.Transform(new Point(p.X, p.Y));

    private ImagePoint ScreenToImage(Point p)
    {
        if (!ViewMatrix.TryInvert(out var inverse))
            return new ImagePoint(0, 0);
        var t = inverse.Transform(p);
        return new ImagePoint(t.X, t.Y);
    }

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
        if (_bitmap is null)
            return;

        EnsureFitted();
        context.Custom(new SkiaImageDrawOperation(
            new Rect(Bounds.Size), _bitmap, ViewMatrix, Brightness, Contrast));

        foreach (var (a, b) in _traceLines)
            context.DrawLine(TracePen, ImageToScreen(a), ImageToScreen(b));

        foreach (var landmark in _landmarks)
        {
            var p = ImageToScreen(landmark.Point);
            var brush = landmark.IsCurrent ? CurrentBrush : PlacedBrush;
            context.DrawEllipse(brush, null, p, 3, 3);
            DrawLabel(context, landmark.Code, p, brush);
        }

        DrawMagnifier(context);
    }

    private void DrawMagnifier(DrawingContext context)
    {
        if (_bitmap is null || !_placementActive || _hoverScreen is not { } hover || _panning)
            return;

        var imagePoint = ScreenToImage(hover);
        var magnifierRect = new Rect(Bounds.Width - MagnifierSize - 12, 12, MagnifierSize, MagnifierSize);

        var magnifierMatrix =
            Matrix.CreateTranslation(-imagePoint.X, -imagePoint.Y)
            * Matrix.CreateScale(_zoom * MagnifierFactor, _zoom * MagnifierFactor)
            * Matrix.CreateTranslation(magnifierRect.Center.X, magnifierRect.Center.Y);

        context.FillRectangle(BackgroundBrush, magnifierRect);
        context.Custom(new SkiaImageDrawOperation(
            new Rect(Bounds.Size), _bitmap, magnifierMatrix, Brightness, Contrast, magnifierRect));

        var center = magnifierRect.Center;
        context.DrawLine(CrosshairPen,
            new Point(center.X - 12, center.Y), new Point(center.X + 12, center.Y));
        context.DrawLine(CrosshairPen,
            new Point(center.X, center.Y - 12), new Point(center.X, center.Y + 12));
        context.DrawRectangle(null, MagnifierBorderPen, magnifierRect);
    }

    private static void DrawLabel(DrawingContext context, string text, Point anchor, IBrush brush)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, weight: FontWeight.Bold), 13, brush);
        context.DrawText(formatted, new Point(anchor.X + 6, anchor.Y - formatted.Height - 2));
    }

    private string? HitLandmark(Point screen)
    {
        foreach (var landmark in _landmarks)
        {
            var p = ImageToScreen(landmark.Point);
            if (Math.Abs(p.X - screen.X) <= GrabRadius && Math.Abs(p.Y - screen.Y) <= GrabRadius)
                return landmark.Code;
        }
        return null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_bitmap is null)
            return;

        var pos = e.GetPosition(this);
        var anchor = ScreenToImage(pos);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.15, e.Delta.Y), 0.02, 50);
        var newScreen = ImageToScreen(anchor);
        _pan = new Point(_pan.X + pos.X - newScreen.X, _pan.Y + pos.Y - newScreen.Y);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_bitmap is null)
            return;

        Focus();
        var pos = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsMiddleButtonPressed || properties.IsRightButtonPressed)
        {
            _panning = true;
            _lastPointer = pos;
            e.Pointer.Capture(this);
            return;
        }

        // Saisir un point existant pour le déplacer, sinon placer le point courant.
        if (HitLandmark(pos) is { } grabbed)
        {
            _draggingCode = grabbed;
            e.Pointer.Capture(this);
            return;
        }

        if (_placementActive)
            LandmarkPlaced?.Invoke(ScreenToImage(pos));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_panning)
        {
            _pan = new Point(_pan.X + pos.X - _lastPointer.X, _pan.Y + pos.Y - _lastPointer.Y);
            _lastPointer = pos;
            InvalidateVisual();
            return;
        }

        if (_draggingCode is { } code)
        {
            LandmarkMoved?.Invoke(code, ScreenToImage(pos));
            return;
        }

        _hoverScreen = pos;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _panning = false;
        _draggingCode = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverScreen = null;
        InvalidateVisual();
    }
}
