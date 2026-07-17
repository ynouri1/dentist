using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ortho.Application.Abstractions;
using Ortho.Application.Imaging;

namespace Ortho.Application.Training;

public record TrainingExportResult(int Samples, int Landmarks, string ManifestPath);

/// <summary>
/// Transforme les analyses validées en jeu d'entraînement pour l'IA (P1),
/// UNIQUEMENT pour les patients ayant consenti, et de façon ANONYMISÉE :
/// aucun nom, date de naissance ni numéro de dossier n'est écrit — seul un
/// pseudonyme dérivé (non réversible) de l'identifiant technique.
/// C'est la mitigation du risque R4 : le MVP manuel constitue le dataset local.
/// </summary>
public class TrainingDataExporter(
    IPatientRepository patients, ICephRepository analyses, ImagingService imaging, IAuditTrail audit)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<TrainingExportResult> ExportAsync(string targetDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var samplesDirectory = Path.Combine(targetDirectory, "samples");
        Directory.CreateDirectory(samplesDirectory);

        var manifest = new List<object>();
        var totalLandmarks = 0;

        foreach (var patient in await patients.ListWithResearchConsentAsync(ct))
        {
            var pseudonym = Pseudonymize(patient.Id);

            foreach (var image in patient.Images)
            {
                var imageAnalyses = await analyses.ListByImageAsync(image.Id, ct);
                var placed = imageAnalyses
                    .SelectMany(a => a.Landmarks.Select(l => (a.TemplateCode, l)))
                    .ToList();
                if (placed.Count == 0)
                    continue;

                var full = await patients.GetImageAsync(image.Id, ct) ?? image;
                var sampleDirectory = Path.Combine(samplesDirectory, image.Id.ToString("N"));
                Directory.CreateDirectory(sampleDirectory);

                await using (var display = await imaging.OpenDisplayAsync(full, ct))
                await using (var file = File.Create(Path.Combine(sampleDirectory, "image.png")))
                    await display.CopyToAsync(file, ct);

                // Points dédupliqués par code (le dernier placé fait foi).
                var points = placed
                    .GroupBy(p => p.l.Code)
                    .Select(g => g.OrderByDescending(p => p.l.PlacedAtUtc).First().l)
                    .Select(l => new
                    {
                        code = l.Code,
                        x = l.X,
                        y = l.Y,
                        source = l.Source.ToString(),
                        confidence = l.Confidence,
                    })
                    .ToList();
                totalLandmarks += points.Count;

                var landmarksPath = Path.Combine(sampleDirectory, "landmarks.json");
                await File.WriteAllTextAsync(landmarksPath, JsonSerializer.Serialize(new
                {
                    subject = pseudonym,
                    imageWidth = full.Width,
                    imageHeight = full.Height,
                    pixelSpacingMm = full.PixelSpacingXMm,
                    templates = placed.Select(p => p.TemplateCode).Distinct(),
                    points,
                }, Json), ct);

                manifest.Add(new { subject = pseudonym, sample = image.Id.ToString("N"), landmarks = points.Count });
            }
        }

        var manifestPath = Path.Combine(targetDirectory, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(new
        {
            exportedAtUtc = DateTime.UtcNow,
            samples = manifest.Count,
            note = "Données anonymisées, patients consentants uniquement.",
            items = manifest,
        }, Json), ct);

        await audit.RecordAsync("training.export", "TrainingDataset", manifest.Count.ToString(),
            $"{manifest.Count} échantillon(s), {totalLandmarks} landmark(s)", ct);

        return new TrainingExportResult(manifest.Count, totalLandmarks, manifestPath);
    }

    /// <summary>Pseudonyme non réversible : 12 hex de SHA-256(id). Ne permet pas de remonter au patient.</summary>
    private static string Pseudonymize(Guid patientId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(patientId.ToString("N")));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}
