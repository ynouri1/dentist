using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ortho.Integration.Tests;

public class CephAnalysisTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N"));

    private ServiceProvider BuildProvider()
    {
        var provider = new ServiceCollection()
            .AddOrthoInfrastructure(new OrthoDataOptions(_dataDirectory))
            .BuildServiceProvider();

        using var db = provider.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext();
        db.Database.Migrate();
        return provider;
    }

    private static async Task<MedicalImage> ImportImageAsync(ServiceProvider provider)
    {
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();
        var patient = await patients.CreateAsync(new PatientDraft { FirstName = "Selim", LastName = "Ayadi" });

        using var image = new Image<Rgb24>(60, 60);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        return await imaging.ImportAsync(patient.Id, stream, "cephalo.jpg");
    }

    [Fact]
    public async Task Analysis_is_created_once_per_image_and_template()
    {
        await using var provider = BuildProvider();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var image = await ImportImageAsync(provider);

        var first = await ceph.GetOrCreateAsync(image.Id, "steiner");
        var second = await ceph.GetOrCreateAsync(image.Id, "steiner");
        var tweed = await ceph.GetOrCreateAsync(image.Id, "tweed");

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Id, tweed.Id);
        Assert.Equal("1.0", first.TemplateVersion);
    }

    [Fact]
    public async Task Landmarks_are_upserted_persisted_and_removable()
    {
        await using var provider = BuildProvider();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var image = await ImportImageAsync(provider);
        var analysis = await ceph.GetOrCreateAsync(image.Id, "steiner");

        await ceph.SetLandmarkAsync(analysis.Id, "S", new ImagePoint(10, 20));
        await ceph.SetLandmarkAsync(analysis.Id, "N", new ImagePoint(30, 40));
        // Repositionnement : même code → mise à jour, pas de doublon.
        await ceph.SetLandmarkAsync(analysis.Id, "S", new ImagePoint(11, 21));

        var reloaded = await ceph.GetAsync(analysis.Id);
        Assert.Equal(2, reloaded!.Landmarks.Count);
        var s = reloaded.Landmarks.Single(l => l.Code == "S");
        Assert.Equal(11, s.X);
        Assert.Equal(21, s.Y);
        Assert.Equal(LandmarkSource.Manual, s.Source);

        await ceph.RemoveLandmarkAsync(analysis.Id, "S");
        reloaded = await ceph.GetAsync(analysis.Id);
        Assert.Single(reloaded!.Landmarks);
    }

    [Fact]
    public async Task Unknown_landmark_code_is_rejected()
    {
        await using var provider = BuildProvider();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var image = await ImportImageAsync(provider);
        var analysis = await ceph.GetOrCreateAsync(image.Id, "tweed");

        await Assert.ThrowsAsync<ValidationException>(
            () => ceph.SetLandmarkAsync(analysis.Id, "XYZ", new ImagePoint(0, 0)));
    }

    [Fact]
    public async Task Deleting_image_cascades_to_analyses_and_landmarks()
    {
        await using var provider = BuildProvider();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var imaging = provider.GetRequiredService<ImagingService>();
        var image = await ImportImageAsync(provider);
        var analysis = await ceph.GetOrCreateAsync(image.Id, "steiner");
        await ceph.SetLandmarkAsync(analysis.Id, "S", new ImagePoint(1, 2));

        await imaging.DeleteImageAsync(image.Id);

        await using var db = await provider
            .GetRequiredService<IDbContextFactory<OrthoDbContext>>()
            .CreateDbContextAsync();
        Assert.Empty(db.CephAnalyses.Where(a => a.MedicalImageId == image.Id));
        Assert.Empty(db.CephLandmarks.Where(l => l.AnalysisId == analysis.Id));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataDirectory))
                Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Fichiers encore verrouillés : le répertoire temporaire sera nettoyé par l'OS.
        }
    }
}
