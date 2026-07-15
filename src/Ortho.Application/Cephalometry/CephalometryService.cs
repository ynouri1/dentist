using Ortho.Application.Abstractions;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Cephalometry;
using Ortho.Domain.Entities;

namespace Ortho.Application.Cephalometry;

public enum MeasureStatus
{
    /// <summary>|écart| ≤ 1 écart-type.</summary>
    Normal = 0,
    /// <summary>1 &lt; |écart| ≤ 2 écarts-types.</summary>
    Borderline = 1,
    /// <summary>|écart| &gt; 2 écarts-types.</summary>
    Outside = 2,
    /// <summary>Landmarks manquants ou calibration requise.</summary>
    NotComputable = 3,
}

public record MeasureResult(
    string Code,
    string Name,
    string Unit,
    double? Value,
    double NormMean,
    double NormSd,
    MeasureStatus Status)
{
    /// <summary>Écart à la norme en écarts-types, null si non calculable.</summary>
    public double? DeviationSd => Value is { } v && NormSd > 0 ? (v - NormMean) / NormSd : null;
}

public class CephalometryService(ICephRepository analyses, IAuditTrail audit)
{
    /// <summary>Une analyse par couple image/template : retrouvée ou créée.</summary>
    public async Task<CephAnalysis> GetOrCreateAsync(Guid imageId, string templateCode, CancellationToken ct = default)
    {
        var existing = await analyses.GetByImageAndTemplateAsync(imageId, templateCode, ct);
        if (existing is not null)
            return existing;

        var template = AnalysisTemplates.Get(templateCode);
        var analysis = new CephAnalysis
        {
            MedicalImageId = imageId,
            TemplateCode = template.Code,
            TemplateVersion = template.Version,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await analyses.AddAsync(analysis, ct);
        await audit.RecordAsync("ceph.create", nameof(CephAnalysis), analysis.Id.ToString(),
            $"{template.Code} v{template.Version} image {imageId}", ct);
        return analysis;
    }

    public Task<CephAnalysis?> GetAsync(Guid analysisId, CancellationToken ct = default)
        => analyses.GetAsync(analysisId, ct);

    public async Task SetLandmarkAsync(
        Guid analysisId, string code, ImagePoint point,
        LandmarkSource source = LandmarkSource.Manual, double? confidence = null, CancellationToken ct = default)
    {
        if (!LandmarkCatalog.All.ContainsKey(code))
            throw new ValidationException([$"Landmark inconnu : {code}"]);

        await analyses.UpsertLandmarkAsync(new CephLandmark
        {
            AnalysisId = analysisId,
            Code = code,
            X = point.X,
            Y = point.Y,
            Source = source,
            Confidence = confidence,
            PlacedAtUtc = DateTime.UtcNow,
        }, ct);
    }

    public Task RemoveLandmarkAsync(Guid analysisId, string code, CancellationToken ct = default)
        => analyses.DeleteLandmarkAsync(analysisId, code, ct);

    /// <summary>
    /// Calcule toutes les mesures du template. Pur et déterministe : c'est la
    /// partie couverte par les tests contre les cas publiés (risque R2).
    /// Les angles utilisent les coordonnées physiques (spacing anisotrope) ;
    /// les distances en mm exigent une image calibrée.
    /// </summary>
    public static IReadOnlyList<MeasureResult> ComputeResults(
        AnalysisTemplate template,
        IReadOnlyDictionary<string, ImagePoint> landmarks,
        double? spacingXMm,
        double? spacingYMm)
    {
        var calibrated = spacingXMm is > 0 && spacingYMm is > 0;
        // Pour les angles, un spacing relatif suffit ; sans calibration on
        // suppose des pixels carrés (vrai pour tous les capteurs courants).
        var sx = spacingXMm is > 0 ? spacingXMm.Value : 1;
        var sy = spacingYMm is > 0 ? spacingYMm.Value : 1;

        var values = new Dictionary<string, double>();
        var results = new List<MeasureResult>();

        foreach (var measure in template.Measures)
        {
            double? value = measure.Kind switch
            {
                MeasureKind.AngleAtVertex when TryGet(measure.Landmarks, landmarks, out var p)
                    => GeometryCalculator.AngleDegrees(p[0], p[1], p[2], sx, sy),

                MeasureKind.AngleBetweenLines when TryGet(measure.Landmarks, landmarks, out var p)
                    => AngleBetweenVectors(p[0], p[1], p[2], p[3], sx, sy),

                MeasureKind.Distance when calibrated && TryGet(measure.Landmarks, landmarks, out var p)
                    => GeometryCalculator.Distance(p[0], p[1], sx, sy),

                MeasureKind.PointToLineDistance when calibrated && TryGet(measure.Landmarks, landmarks, out var p)
                    => PointToLine(p[0], p[1], p[2], sx, sy),

                MeasureKind.Difference when Operands(measure, values) is { } o
                    => o[0] - o[1],

                MeasureKind.Supplement when Operands(measure, values) is { } o
                    => 180 - o.Sum(),

                _ => null,
            };

            if (value is { } v)
                values[measure.Code] = v;

            results.Add(new MeasureResult(
                measure.Code, measure.Name, measure.Unit, value,
                measure.NormMean, measure.NormSd, StatusOf(value, measure)));
        }

        return results;
    }

    private static bool TryGet(
        string[] codes, IReadOnlyDictionary<string, ImagePoint> landmarks, out ImagePoint[] points)
    {
        points = new ImagePoint[codes.Length];
        for (var i = 0; i < codes.Length; i++)
        {
            if (!landmarks.TryGetValue(codes[i], out points[i]))
                return false;
        }
        return true;
    }

    private static double[]? Operands(MeasureDefinition measure, Dictionary<string, double> values)
    {
        if (measure.Operands is not { Length: > 0 } codes)
            return null;
        var result = new double[codes.Length];
        for (var i = 0; i < codes.Length; i++)
        {
            if (!values.TryGetValue(codes[i], out result[i]))
                return null;
        }
        return result;
    }

    /// <summary>Angle entre les vecteurs P1→P2 et P3→P4 en coordonnées physiques, 0–180°.</summary>
    private static double AngleBetweenVectors(
        ImagePoint p1, ImagePoint p2, ImagePoint p3, ImagePoint p4, double sx, double sy)
    {
        var (uX, uY) = ((p2.X - p1.X) * sx, (p2.Y - p1.Y) * sy);
        var (vX, vY) = ((p4.X - p3.X) * sx, (p4.Y - p3.Y) * sy);
        var normU = Math.Sqrt(uX * uX + uY * uY);
        var normV = Math.Sqrt(vX * vX + vY * vY);
        if (normU == 0 || normV == 0)
            return 0;
        var cos = Math.Clamp((uX * vX + uY * vY) / (normU * normV), -1, 1);
        return Math.Acos(cos) * 180 / Math.PI;
    }

    /// <summary>Distance perpendiculaire du point à la droite (l1,l2) en coordonnées physiques.</summary>
    private static double PointToLine(ImagePoint point, ImagePoint l1, ImagePoint l2, double sx, double sy)
    {
        var (px, py) = (point.X * sx, point.Y * sy);
        var (ax, ay) = (l1.X * sx, l1.Y * sy);
        var (bx, by) = (l2.X * sx, l2.Y * sy);
        var length = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        if (length == 0)
            return 0;
        return Math.Abs((bx - ax) * (ay - py) - (ax - px) * (by - ay)) / length;
    }

    private static MeasureStatus StatusOf(double? value, MeasureDefinition measure)
    {
        if (value is not { } v)
            return MeasureStatus.NotComputable;
        // Epsilon : une valeur à exactement ±1 σ ou ±2 σ ne doit pas basculer
        // de catégorie sur un bruit d'arrondi flottant.
        const double epsilon = 1e-9;
        var deviation = Math.Abs(v - measure.NormMean) / measure.NormSd;
        return deviation switch
        {
            _ when deviation <= 1 + epsilon => MeasureStatus.Normal,
            _ when deviation <= 2 + epsilon => MeasureStatus.Borderline,
            _ => MeasureStatus.Outside,
        };
    }
}
