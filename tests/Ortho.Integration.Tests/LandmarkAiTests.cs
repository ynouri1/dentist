using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Abstractions;
using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Application.Training;
using Ortho.Domain.Entities;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ortho.Integration.Tests;

public class LandmarkAiTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N"));
    private readonly string _exportDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N") + "-ds");

    private ServiceProvider BuildProvider()
    {
        var provider = new ServiceCollection()
            .AddOrthoInfrastructure(new OrthoDataOptions(_dataDirectory))
            .BuildServiceProvider();
        using var db = provider.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext();
        db.Database.Migrate();
        return provider;
    }

    private static async Task<(Patient, MedicalImage)> SeedAsync(ServiceProvider provider, bool consent)
    {
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();
        var patient = await patients.CreateAsync(new PatientDraft
        {
            FirstName = "Data", LastName = "Sujet", ResearchConsent = consent,
        });
        using var picture = new Image<Rgb24>(64, 64);
        using var stream = new MemoryStream();
        picture.SaveAsPng(stream);
        stream.Position = 0;
        var image = await imaging.ImportAsync(patient.Id, stream, "cephalo.png");
        return (patient, image);
    }

    [Fact]
    public void Detector_is_unavailable_and_returns_nothing_without_a_model()
    {
        using var provider = BuildProvider();
        var detector = provider.GetRequiredService<ILandmarkDetector>();

        Assert.False(detector.IsAvailable);
        var result = detector.DetectAsync([1, 2, 3], ["S", "N"]).GetAwaiter().GetResult();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Applying_detected_landmarks_marks_them_as_ai_with_confidence()
    {
        await using var provider = BuildProvider();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var (_, image) = await SeedAsync(provider, consent: false);
        var analysis = await ceph.GetOrCreateAsync(image.Id, "steiner");

        await ceph.ApplyDetectedLandmarksAsync(analysis.Id,
        [
            new DetectedLandmark("S", new ImagePoint(10, 12), 0.88),
            new DetectedLandmark("N", new ImagePoint(30, 14), 0.91),
        ]);

        var reloaded = await ceph.GetAsync(analysis.Id);
        var s = reloaded!.Landmarks.Single(l => l.Code == "S");
        Assert.Equal(LandmarkSource.Ai, s.Source);
        Assert.Equal(0.88, s.Confidence!.Value, precision: 6);

        await using var db = await provider
            .GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContextAsync();
        Assert.Single(db.AuditEntries.Where(a => a.Action == "ceph.ai.preplace"));
    }

    [Fact]
    public async Task Training_export_only_includes_consenting_patients_and_is_anonymized()
    {
        await using var provider = BuildProvider();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var exporter = provider.GetRequiredService<TrainingDataExporter>();

        var (withConsent, consentImage) = await SeedAsync(provider, consent: true);
        var analysis = await ceph.GetOrCreateAsync(consentImage.Id, "steiner");
        await ceph.SetLandmarkAsync(analysis.Id, "S", new ImagePoint(5, 6));
        await ceph.SetLandmarkAsync(analysis.Id, "N", new ImagePoint(7, 8));

        // Patient sans consentement : ne doit PAS apparaître.
        var (_, noConsentImage) = await SeedAsync(provider, consent: false);
        var other = await ceph.GetOrCreateAsync(noConsentImage.Id, "steiner");
        await ceph.SetLandmarkAsync(other.Id, "S", new ImagePoint(1, 1));

        var result = await exporter.ExportAsync(_exportDirectory);

        Assert.Equal(1, result.Samples);
        Assert.Equal(2, result.Landmarks);

        var manifest = await File.ReadAllTextAsync(result.ManifestPath);
        // Aucune donnée identifiante : pas de nom ni de numéro de dossier.
        Assert.DoesNotContain("Sujet", manifest);
        Assert.DoesNotContain(withConsent.FileNumber, manifest);

        var landmarksJson = await File.ReadAllTextAsync(
            Path.Combine(_exportDirectory, "samples", consentImage.Id.ToString("N"), "landmarks.json"));
        Assert.Contains("\"code\": \"S\"", landmarksJson);
        Assert.True(File.Exists(
            Path.Combine(_exportDirectory, "samples", consentImage.Id.ToString("N"), "image.png")));

        await using var db = await provider
            .GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContextAsync();
        Assert.Single(db.AuditEntries.Where(a => a.Action == "training.export"));
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _dataDirectory, _exportDirectory })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
        }
    }
}
