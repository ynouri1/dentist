using Ortho.Application.Abstractions;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.Application.Imaging;

public class ImagingService(
    IPatientRepository patients, IObjectStore store, IMedicalImageDecoder decoder, IAuditTrail audit)
{
    public async Task<MedicalImage> ImportAsync(
        Guid patientId, Stream content, string fileName, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        DecodedImage decoded;
        try
        {
            decoded = await decoder.DecodeAsync(bytes, fileName, ct);
        }
        catch (ImageDecodeException ex)
        {
            await audit.RecordAsync("image.import.failed", nameof(MedicalImage), fileName,
                $"patient {patientId} : {ex.Message}", ct);
            throw;
        }

        var image = new MedicalImage
        {
            PatientId = patientId,
            FileName = Path.GetFileName(fileName),
            Modality = decoded.Modality,
            SourceDescription = decoded.SourceDescription,
            Width = decoded.Width,
            Height = decoded.Height,
            PixelSpacingXMm = decoded.PixelSpacingXMm,
            PixelSpacingYMm = decoded.PixelSpacingYMm,
            CalibrationSource = decoded.PixelSpacingXMm is > 0 && decoded.PixelSpacingYMm is > 0
                ? CalibrationSource.Dicom
                : CalibrationSource.None,
            SizeBytes = bytes.Length,
            AcquiredAt = decoded.AcquiredAt,
            ImportedAtUtc = DateTime.UtcNow,
        };
        image.StorageKeyOriginal = $"patients/{patientId:N}/images/{image.Id:N}/original";
        image.StorageKeyDisplay = $"patients/{patientId:N}/images/{image.Id:N}/display";

        await store.SaveAsync(image.StorageKeyOriginal, new MemoryStream(bytes), ct);
        await store.SaveAsync(image.StorageKeyDisplay, new MemoryStream(decoded.DisplayPng), ct);
        await patients.AddImageAsync(image, ct);

        await audit.RecordAsync("image.import", nameof(MedicalImage), image.Id.ToString(),
            $"{image.FileName} ({image.Modality ?? "raster"}) patient {patientId}", ct);
        return image;
    }

    /// <summary>Calibration par règle étalon : une longueur connue mesurée en pixels sur l'image.</summary>
    public async Task<MedicalImage> CalibrateManuallyAsync(
        Guid imageId, double knownLengthMm, double measuredLengthPx, CancellationToken ct = default)
    {
        var spacing = ComputeSpacing(knownLengthMm, measuredLengthPx);

        var image = await patients.GetImageAsync(imageId, ct)
            ?? throw new InvalidOperationException($"Image introuvable : {imageId}");

        image.PixelSpacingXMm = spacing;
        image.PixelSpacingYMm = spacing;
        image.CalibrationSource = CalibrationSource.Manual;
        await patients.UpdateImageAsync(image, ct);

        await audit.RecordAsync("image.calibrate", nameof(MedicalImage), image.Id.ToString(),
            $"{spacing:F6} mm/px (règle {knownLengthMm} mm / {measuredLengthPx} px)", ct);
        return image;
    }

    public static double ComputeSpacing(double knownLengthMm, double measuredLengthPx)
    {
        if (knownLengthMm <= 0 || measuredLengthPx <= 0)
            throw new ValidationException(["La calibration exige des longueurs strictement positives."]);
        return knownLengthMm / measuredLengthPx;
    }

    public Task<Stream> OpenDisplayAsync(MedicalImage image, CancellationToken ct = default)
        => store.OpenReadAsync(image.StorageKeyDisplay, ct);

    public Task<Stream> OpenOriginalAsync(MedicalImage image, CancellationToken ct = default)
        => store.OpenReadAsync(image.StorageKeyOriginal, ct);
}
