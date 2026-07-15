namespace Ortho.Domain.Entities;

public enum LandmarkSource
{
    Manual = 0,
    /// <summary>Proposé par l'IA (P1) — devra être validé par le praticien.</summary>
    Ai = 1,
}

/// <summary>Analyse céphalométrique menée sur une image : un jeu de landmarks placés.</summary>
public class CephAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicalImageId { get; set; }

    /// <summary>Code du template (steiner, tweed…) + version au moment de la création.</summary>
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<CephLandmark> Landmarks { get; set; } = [];
}

/// <summary>Landmark placé, avec provenance — la traçabilité exigée par le futur dossier MDR.</summary>
public class CephLandmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }

    public string Code { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }

    public LandmarkSource Source { get; set; }
    /// <summary>Score de confiance IA (P1) ; null pour un placement manuel.</summary>
    public double? Confidence { get; set; }

    public DateTime PlacedAtUtc { get; set; }
}
