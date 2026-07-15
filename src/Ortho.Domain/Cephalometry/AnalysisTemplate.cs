namespace Ortho.Domain.Cephalometry;

/// <summary>Point céphalométrique : code court universel + libellé.</summary>
public record LandmarkDefinition(string Code, string Name);

public enum MeasureKind
{
    /// <summary>Angle au sommet : landmarks [a, sommet, b].</summary>
    AngleAtVertex = 0,
    /// <summary>Angle entre les vecteurs P1→P2 et P3→P4 : landmarks [p1, p2, p3, p4], résultat 0–180°.</summary>
    AngleBetweenLines = 1,
    /// <summary>Distance entre deux points (mm, exige la calibration).</summary>
    Distance = 2,
    /// <summary>Distance perpendiculaire d'un point à une droite : landmarks [point, l1, l2] (mm).</summary>
    PointToLineDistance = 3,
    /// <summary>Différence entre deux autres mesures : Operands = [code1, code2] → v1 − v2.</summary>
    Difference = 4,
    /// <summary>Complément à 180° de la somme d'autres mesures : 180 − Σ Operands (triangle de Tweed).</summary>
    Supplement = 5,
}

/// <summary>
/// Mesure déclarative : landmarks requis, formule (Kind), norme ± écart-type.
/// Les normes sont des DONNÉES, revues par l'orthodontiste référent, jamais du code.
/// </summary>
public record MeasureDefinition(
    string Code,
    string Name,
    MeasureKind Kind,
    string[] Landmarks,
    string Unit,
    double NormMean,
    double NormSd,
    string[]? Operands = null);

/// <summary>Analyse céphalométrique complète, versionnée pour la traçabilité.</summary>
public record AnalysisTemplate(
    string Code,
    string Name,
    string Version,
    IReadOnlyList<MeasureDefinition> Measures)
{
    /// <summary>Landmarks requis par l'ensemble des mesures, dans l'ordre de première utilisation.</summary>
    public IReadOnlyList<string> RequiredLandmarks { get; } =
        Measures.SelectMany(m => m.Landmarks).Distinct().ToList();
}
