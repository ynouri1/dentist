namespace Ortho.Application.Abstractions;

/// <summary>Résultat du décodage d'un fichier image (DICOM ou raster).</summary>
public record DecodedImage(
    byte[] DisplayPng,
    int Width,
    int Height,
    double? PixelSpacingXMm,
    double? PixelSpacingYMm,
    string? Modality,
    string? SourceDescription,
    DateTime? AcquiredAt);

/// <summary>
/// Levée quand un fichier ne peut pas être décodé (DICOM exotique, fichier
/// corrompu…). Le message est montré au praticien avec la marche à suivre de
/// repli : exporter en JPG depuis l'appareil et réimporter.
/// </summary>
public class ImageDecodeException(string message, Exception? inner = null) : Exception(message, inner);

public interface IMedicalImageDecoder
{
    /// <summary>Décode un DICOM ou un JPG/PNG/TIFF. Lève <see cref="ImageDecodeException"/> si illisible.</summary>
    Task<DecodedImage> DecodeAsync(byte[] content, string fileName, CancellationToken ct = default);
}
