namespace Ortho.Domain.Cephalometry;

/// <summary>
/// Définitions des analyses livrées. Normes adultes issues des publications
/// originales (Steiner 1953, Tweed 1954) — à faire valider par l'orthodontiste
/// référent avant la mise en production (risque R2).
/// </summary>
public static class AnalysisTemplates
{
    public static readonly AnalysisTemplate Steiner = new(
        "steiner", "Analyse de Steiner", "1.0",
        [
            new("SNA", "SNA", MeasureKind.AngleAtVertex, ["S", "N", "A"], "°", 82, 2),
            new("SNB", "SNB", MeasureKind.AngleAtVertex, ["S", "N", "B"], "°", 80, 2),
            new("ANB", "ANB", MeasureKind.Difference, [], "°", 2, 2, Operands: ["SNA", "SNB"]),
            new("SND", "SND", MeasureKind.AngleAtVertex, ["S", "N", "D"], "°", 76, 2),
            new("SN-GoGn", "Plan mandibulaire SN–GoGn", MeasureKind.AngleBetweenLines,
                ["S", "N", "Go", "Gn"], "°", 32, 5),
            new("U1-NA-angle", "Incisive sup. / NA (angle)", MeasureKind.AngleBetweenLines,
                ["U1A", "U1E", "N", "A"], "°", 22, 5),
            new("U1-NA-mm", "Incisive sup. / NA (distance)", MeasureKind.PointToLineDistance,
                ["U1E", "N", "A"], "mm", 4, 2),
            new("L1-NB-angle", "Incisive inf. / NB (angle)", MeasureKind.AngleBetweenLines,
                ["L1A", "L1E", "N", "B"], "°", 25, 5),
            new("L1-NB-mm", "Incisive inf. / NB (distance)", MeasureKind.PointToLineDistance,
                ["L1E", "N", "B"], "mm", 4, 2),
            new("Pog-NB", "Pogonion / NB (distance)", MeasureKind.PointToLineDistance,
                ["Pog", "N", "B"], "mm", 2, 2),
            new("Interincisal", "Angle interincisif", MeasureKind.AngleBetweenLines,
                ["U1E", "U1A", "L1E", "L1A"], "°", 131, 6),
        ]);

    public static readonly AnalysisTemplate Tweed = new(
        "tweed", "Analyse de Tweed", "1.0",
        [
            new("FMA", "FMA (Francfort / plan mandibulaire)", MeasureKind.AngleBetweenLines,
                ["Po", "Or", "Go", "Me"], "°", 25, 4),
            new("IMPA", "IMPA (incisive inf. / plan mandibulaire)", MeasureKind.AngleBetweenLines,
                ["L1A", "L1E", "Me", "Go"], "°", 90, 5),
            new("FMIA", "FMIA (Francfort / incisive inf.)", MeasureKind.Supplement,
                [], "°", 65, 5, Operands: ["FMA", "IMPA"]),
        ]);

    /// <summary>Sous-ensemble MVP de Ricketts (l'analyse complète compte 30+ mesures).</summary>
    public static readonly AnalysisTemplate Ricketts = new(
        "ricketts", "Analyse de Ricketts", "1.0",
        [
            new("FacialDepth", "Profondeur faciale (FH / N-Pog)", MeasureKind.AngleBetweenLines,
                ["Po", "Or", "N", "Pog"], "°", 87, 3),
            new("MandPlane", "Plan mandibulaire / FH", MeasureKind.AngleBetweenLines,
                ["Po", "Or", "Go", "Me"], "°", 26, 4),
            new("ConvexityA", "Convexité du point A (A → N-Pog)", MeasureKind.PointToLineDistance,
                ["A", "N", "Pog"], "mm", 2, 2),
            new("L1-APog-mm", "Incisive inf. / A-Pog (distance)", MeasureKind.PointToLineDistance,
                ["L1E", "A", "Pog"], "mm", 1, 2.3),
            new("L1-APog-angle", "Incisive inf. / A-Pog (angle)", MeasureKind.AngleBetweenLines,
                ["L1A", "L1E", "A", "Pog"], "°", 22, 4),
        ]);

    public static readonly AnalysisTemplate McNamara = new(
        "mcnamara", "Analyse de McNamara", "1.0",
        [
            new("Co-A", "Longueur maxillaire effective (Co–A)", MeasureKind.Distance,
                ["Co", "A"], "mm", 99, 6),
            new("Co-Gn", "Longueur mandibulaire effective (Co–Gn)", MeasureKind.Distance,
                ["Co", "Gn"], "mm", 125, 8),
            new("MaxMandDiff", "Différentiel maxillo-mandibulaire", MeasureKind.Difference,
                [], "mm", 27, 4, Operands: ["Co-Gn", "Co-A"]),
            new("ANS-Me", "Hauteur faciale antérieure inférieure", MeasureKind.Distance,
                ["ANS", "Me"], "mm", 66, 5),
            new("A-NPerp", "Point A / perpendiculaire de Nasion", MeasureKind.PerpendicularDistance,
                ["A", "N", "Po", "Or"], "mm", 1, 2),
            new("Pog-NPerp", "Pogonion / perpendiculaire de Nasion", MeasureKind.PerpendicularDistance,
                ["Pog", "N", "Po", "Or"], "mm", 0, 4),
        ]);

    public static readonly AnalysisTemplate Downs = new(
        "downs", "Analyse de Downs", "1.0",
        [
            new("FacialAngle", "Angle facial (FH / N-Pog)", MeasureKind.AngleBetweenLines,
                ["Po", "Or", "N", "Pog"], "°", 87.8, 3.6),
            new("NAPog", "N-A-Pog (angle interne)", MeasureKind.AngleAtVertex,
                ["N", "A", "Pog"], "°", 180, 5.1, Hidden: true),
            new("Convexity", "Angle de convexité", MeasureKind.Supplement,
                [], "°", 0, 5.1, Operands: ["NAPog"]),
            new("ABPlane", "Plan A-B / N-Pog", MeasureKind.AngleBetweenLines,
                ["A", "B", "N", "Pog"], "°", 4.6, 3.7),
            new("FMA", "FMA (Francfort / plan mandibulaire)", MeasureKind.AngleBetweenLines,
                ["Po", "Or", "Go", "Me"], "°", 21.9, 3.2),
            new("YAxis", "Axe Y (S-Gn / FH)", MeasureKind.AngleBetweenLines,
                ["S", "Gn", "Po", "Or"], "°", 59.4, 3.8),
            new("Interincisal", "Angle interincisif", MeasureKind.AngleBetweenLines,
                ["U1E", "U1A", "L1E", "L1A"], "°", 135.4, 5.8),
        ]);

    public static readonly AnalysisTemplate Jarabak = new(
        "jarabak", "Analyse de Jarabak", "1.0",
        [
            new("Saddle", "Angle de la selle (N-S-Ar)", MeasureKind.AngleAtVertex,
                ["N", "S", "Ar"], "°", 123, 5),
            new("Articular", "Angle articulaire (S-Ar-Go)", MeasureKind.AngleAtVertex,
                ["S", "Ar", "Go"], "°", 143, 6),
            new("Gonial", "Angle goniaque (Ar-Go-Me)", MeasureKind.AngleAtVertex,
                ["Ar", "Go", "Me"], "°", 130, 7),
            new("SumAngles", "Somme des angles du polygone", MeasureKind.Sum,
                [], "°", 396, 6, Operands: ["Saddle", "Articular", "Gonial"]),
            new("FacialHeightRatio", "Rapport hauteurs faciales S-Go / N-Me", MeasureKind.Ratio,
                ["S", "Go", "N", "Me"], "%", 64, 2),
        ]);

    public static readonly IReadOnlyList<AnalysisTemplate> All =
        [Steiner, Ricketts, Tweed, McNamara, Downs, Jarabak];

    public static AnalysisTemplate Get(string code)
        => All.FirstOrDefault(t => t.Code == code)
           ?? throw new KeyNotFoundException($"Analyse inconnue : {code}");
}
