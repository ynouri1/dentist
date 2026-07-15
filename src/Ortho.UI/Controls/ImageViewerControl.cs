using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Ortho.Application.Imaging;
using Ortho.Domain.Entities;
using SkiaSharp;

namespace Ortho.UI.Controls;

public enum ViewerTool
{
    Pan,
    Distance,
    Angle,
    Line,
    Arrow,
    Text,
    /// <summary>Règle étalon : deux points sur une distance connue → calibration mm/pixel.</summary>
    Calibrate,
}

/// <summary>Annotation prête à l'affichage : points image + libellé calculé (mm, degrés…).</summary>
public record AnnotationDisplay(Guid Id, AnnotationType Type, IReadOnlyList<ImagePoint> Points, string? Label);

/// <summary>
/// Viewer 2D : bitmap rendu par Skia (zoom/pan/rotation + contraste/luminosité
/// appliqués par le GPU à chaque frame), calques de mesures dessinés en espace
/// écran pour garder une épaisseur de trait constante.
/// </summary>
public class ImageViewerControl : Control
{
    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<ImageViewerControl, double>(nameof(Brightness));

    public static readonly StyledProperty<double> ContrastProperty =
        AvaloniaProperty.Register<ImageViewerControl, double>(nameof(Contrast));

    static ImageViewerControl()
    {
        AffectsRender<ImageViewerControl>(BrightnessProperty, ContrastProperty);
    }

    /// <summary>Luminosité de −1 à 1.</summary>
    public double Brightness
    {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    /// <summary>Contraste de −1 à 1.</summary>
    public double Contrast
    {
        get => GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    public ViewerTool Tool { get; set; } = ViewerTool.Pan;

    /// <summary>Points image (pixels) de l'annotation terminée ; le texte vient de la fenêtre.</summary>
    public event Action<AnnotationType, IReadOnlyList<ImagePoint>>? AnnotationCompleted;

    /// <summary>Segment de règle étalon tracé : longueur en pixels image.</summary>
    public event Action<double>? CalibrationMeasured;

    private SKBitmap? _bitmap;
    private IReadOnlyList<AnnotationDisplay> _annotations = [];

    private double _zoom = 1;
    private Point _pan;
    private int _rotationDegrees;
    private bool _fitted;

    private readonly List<ImagePoint> _pending = [];
    private Point? _hoverScreen;
    private bool _panning;
    private Point _lastPointer;

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(16, 16, 16));
    private static readonly Pen MeasurePen = new(Brushes.Cyan, 1.5);
    private static readonly Pen PreviewPen = new(Brushes.Cyan, 1) { DashStyle = DashStyle.Dash };
    private static readonly Pen AnnotationPen = new(Brushes.Yellow, 1.5);

    public ImageViewerControl()
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

    public void SetAnnotations(IReadOnlyList<AnnotationDisplay> annotations)
    {
        _annotations = annotations;
        InvalidateVisual();
    }

    public void RotateBy(int degrees)
    {
        _rotationDegrees = ((_rotationDegrees + degrees) % 360 + 360) % 360;
        _fitted = false;
        InvalidateVisual();
    }

    public void FitToView()
    {
        _fitted = false;
        InvalidateVisual();
    }

    public void CancelPending()
    {
        _pending.Clear();
        _hoverScreen = null;
        InvalidateVisual();
    }

    private Matrix ViewMatrix => _bitmap is null
        ? Matrix.Identity
        : Matrix.CreateTranslation(-_bitmap.Width / 2.0, -_bitmap.Height / 2.0)
          * Matrix.CreateRotation(_rotationDegrees * Math.PI / 180)
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

        var (w, h) = _rotationDegrees is 90 or 270
            ? ((double)_bitmap.Height, (double)_bitmap.Width)
            : ((double)_bitmap.Width, (double)_bitmap.Height);
        _zoom = Math.Min(Bounds.Width / w, Bounds.Height / h) * 0.95;
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

        foreach (var annotation in _annotations)
            DrawAnnotation(context, annotation);
        DrawPending(context);
    }

    private void DrawAnnotation(DrawingContext context, AnnotationDisplay annotation)
    {
        var pts = annotation.Points;
        switch (annotation.Type)
        {
            case AnnotationType.Distance when pts.Count == 2:
            {
                var a = ImageToScreen(pts[0]);
                var b = ImageToScreen(pts[1]);
                context.DrawLine(MeasurePen, a, b);
                DrawMarker(context, a);
                DrawMarker(context, b);
                DrawLabel(context, annotation.Label, Midpoint(a, b), Brushes.Cyan);
                break;
            }
            case AnnotationType.Angle when pts.Count == 3:
            {
                var a = ImageToScreen(pts[0]);
                var vertex = ImageToScreen(pts[1]);
                var b = ImageToScreen(pts[2]);
                context.DrawLine(MeasurePen, vertex, a);
                context.DrawLine(MeasurePen, vertex, b);
                DrawMarker(context, vertex);
                DrawLabel(context, annotation.Label, vertex, Brushes.Cyan);
                break;
            }
            case AnnotationType.Line or AnnotationType.Arrow when pts.Count == 2:
            {
                var a = ImageToScreen(pts[0]);
                var b = ImageToScreen(pts[1]);
                context.DrawLine(AnnotationPen, a, b);
                if (annotation.Type == AnnotationType.Arrow)
                    DrawArrowHead(context, a, b);
                break;
            }
            case AnnotationType.Text when pts.Count == 1:
            {
                var p = ImageToScreen(pts[0]);
                DrawMarker(context, p, Brushes.Yellow);
                DrawLabel(context, annotation.Label, p, Brushes.Yellow);
                break;
            }
        }
    }

    private void DrawPending(DrawingContext context)
    {
        if (_pending.Count == 0 || _hoverScreen is not { } hover)
            return;

        var screenPoints = new List<Point>();
        foreach (var p in _pending)
            screenPoints.Add(ImageToScreen(p));
        screenPoints.Add(hover);

        for (var i = 0; i < screenPoints.Count - 1; i++)
            context.DrawLine(PreviewPen, screenPoints[i], screenPoints[i + 1]);
        foreach (var p in screenPoints)
            DrawMarker(context, p);
    }

    private static void DrawMarker(DrawingContext context, Point p, IBrush? brush = null)
    {
        context.DrawEllipse(brush ?? Brushes.Cyan, null, p, 2.5, 2.5);
    }

    private static void DrawArrowHead(DrawingContext context, Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
            return;
        var (ux, uy) = (dx / length, dy / length);
        const double size = 10;
        var left = new Point(to.X - size * (ux * 0.866 - uy * 0.5), to.Y - size * (uy * 0.866 + ux * 0.5));
        var right = new Point(to.X - size * (ux * 0.866 + uy * 0.5), to.Y - size * (uy * 0.866 - ux * 0.5));
        context.DrawLine(AnnotationPen, to, left);
        context.DrawLine(AnnotationPen, to, right);
    }

    private static void DrawLabel(DrawingContext context, string? text, Point anchor, IBrush brush)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var formatted = new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, weight: FontWeight.SemiBold), 14, brush);
        var origin = new Point(anchor.X + 8, anchor.Y - formatted.Height - 4);
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(160, 16, 16, 16)), null,
            new Rect(origin, new Size(formatted.Width, formatted.Height)).Inflate(3));
        context.DrawText(formatted, origin);
    }

    private static Point Midpoint(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_bitmap is null)
            return;

        var pos = e.GetPosition(this);
        var anchor = ScreenToImage(pos);
        var factor = Math.Pow(1.15, e.Delta.Y);
        _zoom = Math.Clamp(_zoom * factor, 0.02, 50);

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

        // Molette ou bouton droit : pan quel que soit l'outil.
        if (properties.IsMiddleButtonPressed || properties.IsRightButtonPressed || Tool == ViewerTool.Pan)
        {
            _panning = true;
            _lastPointer = pos;
            e.Pointer.Capture(this);
            return;
        }

        var imagePoint = ScreenToImage(pos);
        switch (Tool)
        {
            case ViewerTool.Distance:
                _pending.Add(imagePoint);
                if (_pending.Count == 2)
                    Complete(AnnotationType.Distance);
                break;
            case ViewerTool.Angle:
                _pending.Add(imagePoint);
                if (_pending.Count == 3)
                    Complete(AnnotationType.Angle);
                break;
            case ViewerTool.Line or ViewerTool.Arrow:
                _pending.Add(imagePoint);
                if (_pending.Count == 2)
                    Complete(Tool == ViewerTool.Line ? AnnotationType.Line : AnnotationType.Arrow);
                break;
            case ViewerTool.Text:
                AnnotationCompleted?.Invoke(AnnotationType.Text, [imagePoint]);
                break;
            case ViewerTool.Calibrate:
                _pending.Add(imagePoint);
                if (_pending.Count == 2)
                {
                    var pixels = GeometryCalculator.Distance(_pending[0], _pending[1]);
                    _pending.Clear();
                    _hoverScreen = null;
                    CalibrationMeasured?.Invoke(pixels);
                }
                break;
        }

        _hoverScreen = pos;
        InvalidateVisual();
    }

    private void Complete(AnnotationType type)
    {
        var points = new List<ImagePoint>(_pending);
        _pending.Clear();
        _hoverScreen = null;
        AnnotationCompleted?.Invoke(type, points);
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

        if (_pending.Count > 0)
        {
            _hoverScreen = pos;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning)
        {
            _panning = false;
            e.Pointer.Capture(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            CancelPending();
            e.Handled = true;
        }
    }

}
