using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Ortho.UI.Controls;

/// <summary>
/// Dessine un SKBitmap avec transformation (zoom/pan/rotation) et réglage
/// luminosité/contraste appliqués par le GPU à chaque frame. Partagée par le
/// viewer 2D et le canvas céphalométrique (dont la loupe, via <paramref name="clip"/>).
/// </summary>
internal class SkiaImageDrawOperation(
    Rect bounds, SKBitmap bitmap, Matrix matrix, double brightness, double contrast, Rect? clip = null)
    : ICustomDrawOperation
{
    public Rect Bounds => bounds;

    public void Dispose()
    {
    }

    public bool Equals(ICustomDrawOperation? other) => false;

    public bool HitTest(Point p) => bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Save();
        if (clip is { } clipRect)
            canvas.ClipRect(new SKRect(
                (float)clipRect.X, (float)clipRect.Y,
                (float)clipRect.Right, (float)clipRect.Bottom));
        canvas.Concat(ToSkMatrix(matrix));

        using var paint = new SKPaint { IsAntialias = true, ColorFilter = CreateColorFilter(brightness, contrast) };
        canvas.DrawBitmap(bitmap, 0, 0, paint);
        canvas.Restore();
    }

    private static SKMatrix ToSkMatrix(Matrix m) => new(
        (float)m.M11, (float)m.M21, (float)m.M31,
        (float)m.M12, (float)m.M22, (float)m.M32,
        0, 0, 1);

    private static SKColorFilter CreateColorFilter(double brightness, double contrast)
    {
        // Contraste centré sur le gris moyen, luminosité en décalage pur.
        // La colonne de translation de la matrice Skia est normalisée (1.0 = pleine échelle).
        var factor = (float)Math.Clamp(1 + contrast, 0, 2);
        var offset = (float)(brightness + 0.5 * (1 - factor));
        return SKColorFilter.CreateColorMatrix(
        [
            factor, 0, 0, 0, offset,
            0, factor, 0, 0, offset,
            0, 0, factor, 0, offset,
            0, 0, 0, 1, 0,
        ]);
    }
}
