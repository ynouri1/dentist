namespace Ortho.Domain.Entities;

public enum AnnotationType
{
    Distance = 0,
    Angle = 1,
    Line = 2,
    Arrow = 3,
    Text = 4,
}

/// <summary>
/// Mesure ou annotation posée sur une image. Toujours stockée à part :
/// le fichier image d'origine n'est jamais modifié.
/// Les points sont en coordonnées image (pixels), les valeurs en mm sont
/// recalculées à l'affichage à partir de la calibration courante.
/// </summary>
public class ImageAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicalImageId { get; set; }

    public AnnotationType Type { get; set; }

    /// <summary>Points en pixels image, JSON <c>[[x,y],[x,y]…]</c>.</summary>
    public string PointsJson { get; set; } = string.Empty;
    public string? Text { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
