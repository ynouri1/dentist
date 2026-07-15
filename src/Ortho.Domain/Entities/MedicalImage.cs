namespace Ortho.Domain.Entities;

public enum CalibrationSource
{
    /// <summary>Aucune calibration : les mesures en mm sont impossibles.</summary>
    None = 0,
    /// <summary>Pixel spacing lu dans les métadonnées DICOM.</summary>
    Dicom = 1,
    /// <summary>Règle étalon saisie par le praticien.</summary>
    Manual = 2,
}

public class MedicalImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }

    public string FileName { get; set; } = string.Empty;
    /// <summary>Modalité DICOM (CR, DX, PX…) ; null pour une image simple.</summary>
    public string? Modality { get; set; }
    public string? SourceDescription { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Taille d'un pixel en mm (axe X = colonnes, axe Y = lignes).</summary>
    public double? PixelSpacingXMm { get; set; }
    public double? PixelSpacingYMm { get; set; }
    public CalibrationSource CalibrationSource { get; set; }

    /// <summary>Fichier source préservé tel quel (DICOM, TIFF…), chiffré.</summary>
    public string StorageKeyOriginal { get; set; } = string.Empty;
    /// <summary>Rendu PNG pour l'affichage, chiffré.</summary>
    public string StorageKeyDisplay { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public DateTime? AcquiredAt { get; set; }
    public DateTime ImportedAtUtc { get; set; }

    public List<ImageAnnotation> Annotations { get; set; } = [];

    public bool IsCalibrated => CalibrationSource != CalibrationSource.None
        && PixelSpacingXMm is > 0 && PixelSpacingYMm is > 0;
}
