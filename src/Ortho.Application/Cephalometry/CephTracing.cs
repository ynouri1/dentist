using Ortho.Application.Imaging;
using Ortho.Domain.Cephalometry;

namespace Ortho.Application.Cephalometry;

/// <summary>
/// Construit le tracé céphalométrique d'une analyse : les segments de chaque
/// mesure dont tous les landmarks sont placés. Partagé entre la fenêtre
/// d'analyse et la superposition T0/T1.
/// </summary>
public static class CephTracing
{
    public static IReadOnlyList<(ImagePoint A, ImagePoint B)> BuildSegments(
        AnalysisTemplate template, IReadOnlyDictionary<string, ImagePoint> points)
    {
        var lines = new List<(ImagePoint, ImagePoint)>();
        foreach (var measure in template.Measures)
        {
            var codes = measure.Landmarks;
            if (codes.Length == 0 || codes.Any(c => !points.ContainsKey(c)))
                continue;

            switch (measure.Kind)
            {
                case MeasureKind.AngleAtVertex:
                    lines.Add((points[codes[1]], points[codes[0]]));
                    lines.Add((points[codes[1]], points[codes[2]]));
                    break;
                case MeasureKind.AngleBetweenLines:
                case MeasureKind.Ratio:
                    lines.Add((points[codes[0]], points[codes[1]]));
                    lines.Add((points[codes[2]], points[codes[3]]));
                    break;
                case MeasureKind.Distance:
                    lines.Add((points[codes[0]], points[codes[1]]));
                    break;
                case MeasureKind.PointToLineDistance:
                    lines.Add((points[codes[1]], points[codes[2]]));
                    break;
                case MeasureKind.PerpendicularDistance:
                    lines.Add((points[codes[2]], points[codes[3]]));
                    break;
            }
        }
        return lines;
    }
}
