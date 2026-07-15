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

    public static readonly IReadOnlyList<AnalysisTemplate> All = [Steiner, Tweed];

    public static AnalysisTemplate Get(string code)
        => All.FirstOrDefault(t => t.Code == code)
           ?? throw new KeyNotFoundException($"Analyse inconnue : {code}");
}
