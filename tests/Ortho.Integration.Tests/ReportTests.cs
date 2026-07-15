using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Persistence;
using Ortho.Reporting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ortho.Integration.Tests;

public class ReportTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N"));

    private ServiceProvider BuildProvider()
    {
        var provider = new ServiceCollection()
            .AddOrthoInfrastructure(new OrthoDataOptions(_dataDirectory))
            .AddSingleton<ReportService>()
            .BuildServiceProvider();

        using var db = provider.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext();
        db.Database.Migrate();
        return provider;
    }

    [Fact]
    public async Task Full_steiner_report_is_generated_and_archived_in_patient_record()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();
        var ceph = provider.GetRequiredService<CephalometryService>();
        var reports = provider.GetRequiredService<ReportService>();

        var patient = await patients.CreateAsync(new PatientDraft
        {
            FirstName = "Yosra",
            LastName = "Hammami",
            BirthDate = new DateOnly(2010, 3, 15),
        });

        using var radiography = new Image<Rgb24>(200, 200);
        using var stream = new MemoryStream();
        radiography.SaveAsJpeg(stream);
        stream.Position = 0;
        var image = await imaging.ImportAsync(patient.Id, stream, "teleradio.jpg");
        await imaging.CalibrateManuallyAsync(image.Id, 50, 100); // 0,5 mm/px
        image = (await imaging.GetImageAsync(image.Id))!;

        var analysis = await ceph.GetOrCreateAsync(image.Id, "steiner");
        await ceph.SetLandmarkAsync(analysis.Id, "S", new ImagePoint(40, 40));
        await ceph.SetLandmarkAsync(analysis.Id, "N", new ImagePoint(140, 40));
        await ceph.SetLandmarkAsync(analysis.Id, "A", new ImagePoint(150, 110));
        await ceph.SetLandmarkAsync(analysis.Id, "B", new ImagePoint(145, 150));

        var (pdf, archived) = await reports.GenerateCephReportAsync(image, "steiner");

        // Un vrai PDF…
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // …archivé automatiquement dans le dossier patient, chiffré.
        Assert.Equal("application/pdf", archived.ContentType);
        var record = await patients.GetAsync(patient.Id);
        Assert.Contains(record!.Documents, d => d.Id == archived.Id);

        await using var db = await provider
            .GetRequiredService<IDbContextFactory<OrthoDbContext>>()
            .CreateDbContextAsync();
        Assert.Single(db.AuditEntries.Where(a => a.Action == "report.generate"));
    }

    [Fact]
    public async Task Report_requires_placed_landmarks()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();
        var reports = provider.GetRequiredService<ReportService>();

        var patient = await patients.CreateAsync(new PatientDraft { FirstName = "Karim", LastName = "Sfar" });
        using var picture = new Image<Rgb24>(50, 50);
        using var stream = new MemoryStream();
        picture.SaveAsJpeg(stream);
        stream.Position = 0;
        var image = await imaging.ImportAsync(patient.Id, stream, "radio.jpg");

        await Assert.ThrowsAsync<ValidationException>(
            () => reports.GenerateCephReportAsync(image, "steiner"));
    }

    [Fact]
    public void Tracing_renderer_overlays_landmarks_on_source_image()
    {
        using var source = new Image<Rgb24>(120, 80);
        using var stream = new MemoryStream();
        source.SaveAsPng(stream);

        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["S"] = new(10, 10),
            ["N"] = new(100, 10),
        };
        var rendered = TracingImageRenderer.Render(
            stream.ToArray(), landmarks, [(landmarks["S"], landmarks["N"])]);

        using var output = SixLabors.ImageSharp.Image.Load(rendered);
        Assert.Equal(120, output.Width);
        Assert.Equal(80, output.Height);
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
