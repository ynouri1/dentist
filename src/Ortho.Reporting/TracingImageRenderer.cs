using Ortho.Application.Imaging;
using SkiaSharp;

namespace Ortho.Reporting;

/// <summary>
/// Compose l'image du rapport : radiographie + tracé + landmarks, rendue en PNG.
/// Le fichier source n'est jamais modifié.
/// </summary>
public static class TracingImageRenderer
{
    public static byte[] Render(
        byte[] sourcePng,
        IReadOnlyDictionary<string, ImagePoint> landmarks,
        IReadOnlyList<(ImagePoint A, ImagePoint B)> segments)
    {
        using var source = SKBitmap.Decode(sourcePng)
            ?? throw new InvalidOperationException("Image du rapport illisible.");

        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height));
        var canvas = surface.Canvas;
        canvas.DrawBitmap(source, 0, 0);

        // Épaisseurs et polices proportionnelles à la taille de l'image.
        var scale = Math.Max(1f, source.Width / 800f);

        using var linePaint = new SKPaint
        {
            Color = new SKColor(0, 220, 220),
            StrokeWidth = 1.5f * scale,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
        };
        foreach (var (a, b) in segments)
            canvas.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, linePaint);

        using var pointPaint = new SKPaint
        {
            Color = SKColors.Orange,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        using var font = new SKFont(SKTypeface.Default, 12 * scale);
        using var textPaint = new SKPaint { Color = SKColors.Orange, IsAntialias = true };
        foreach (var (code, point) in landmarks)
        {
            canvas.DrawCircle((float)point.X, (float)point.Y, 3 * scale, pointPaint);
            canvas.DrawText(code, (float)point.X + 5 * scale, (float)point.Y - 4 * scale, font, textPaint);
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }
}
