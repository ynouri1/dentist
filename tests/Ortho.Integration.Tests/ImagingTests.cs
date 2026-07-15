using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Abstractions;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Imaging;
using Ortho.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ortho.Integration.Tests;

public class ImagingTests : IDisposable
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

    /// <summary>Téléradiographie synthétique 16×32, 8 bits, pixel spacing 0,2 mm (lignes) × 0,1 mm (colonnes).</summary>
    private static byte[] CreateSyntheticDicom()
    {
        var dataset = new DicomDataset().NotValidated();
        dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
        dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
        dataset.Add(DicomTag.PatientID, "TEST-ANONYME");
        dataset.Add(DicomTag.Modality, "CR");
        dataset.Add(DicomTag.StudyDescription, "Téléradiographie de profil");
        dataset.Add(DicomTag.StudyDate, new DateTime(2026, 7, 1));
        dataset.Add(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value);
        dataset.Add(DicomTag.Rows, (ushort)32);
        dataset.Add(DicomTag.Columns, (ushort)16);
        dataset.Add(DicomTag.BitsAllocated, (ushort)8);
        dataset.Add(DicomTag.BitsStored, (ushort)8);
        dataset.Add(DicomTag.HighBit, (ushort)7);
        dataset.Add(DicomTag.SamplesPerPixel, (ushort)1);
        dataset.Add(DicomTag.PixelRepresentation, (ushort)0);
        dataset.Add(DicomTag.PixelSpacing, 0.2m, 0.1m);

        var pixels = new byte[32 * 16];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)(i % 256);
        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new MemoryByteBuffer(pixels));

        using var stream = new MemoryStream();
        new DicomFile(dataset).Save(stream);
        return stream.ToArray();
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    [Fact]
    public async Task Dicom_import_extracts_pixels_metadata_and_calibration()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();

        var patient = await patients.CreateAsync(new PatientDraft { FirstName = "Rim", LastName = "Jlassi" });
        var dicomBytes = CreateSyntheticDicom();

        var image = await imaging.ImportAsync(patient.Id, new MemoryStream(dicomBytes), "cephalo.dcm");

        Assert.Equal(16, image.Width);
        Assert.Equal(32, image.Height);
        Assert.Equal("CR", image.Modality);
        Assert.Equal(CalibrationSource.Dicom, image.CalibrationSource);
        Assert.Equal(0.1, image.PixelSpacingXMm!.Value, precision: 6); // colonnes
        Assert.Equal(0.2, image.PixelSpacingYMm!.Value, precision: 6); // lignes
        Assert.Equal(new DateTime(2026, 7, 1), image.AcquiredAt);
        Assert.True(image.IsCalibrated);

        // Le rendu d'affichage est un PNG lisible aux bonnes dimensions.
        await using (var display = await imaging.OpenDisplayAsync(image))
        {
            using var png = await SixLabors.ImageSharp.Image.LoadAsync(display);
            Assert.Equal(16, png.Width);
            Assert.Equal(32, png.Height);
        }

        // L'original DICOM est préservé octet pour octet.
        await using (var original = await imaging.OpenOriginalAsync(image))
        {
            using var buffer = new MemoryStream();
            await original.CopyToAsync(buffer);
            Assert.Equal(dicomBytes, buffer.ToArray());
        }

        // Et l'image est rattachée au dossier patient.
        var record = await patients.GetAsync(patient.Id);
        Assert.Single(record!.Images);
    }

    [Fact]
    public async Task Raster_import_then_manual_calibration()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();

        var patient = await patients.CreateAsync(new PatientDraft { FirstName = "Omar", LastName = "Baccar" });
        var imported = await imaging.ImportAsync(
            patient.Id, new MemoryStream(CreateJpeg(20, 10)), "photo.jpg");

        Assert.Equal(20, imported.Width);
        Assert.Equal(10, imported.Height);
        Assert.Equal(CalibrationSource.None, imported.CalibrationSource);
        Assert.False(imported.IsCalibrated);

        // Règle étalon : 50 mm mesurés sur 100 pixels → 0,5 mm/px.
        var calibrated = await imaging.CalibrateManuallyAsync(imported.Id, 50, 100);
        Assert.Equal(CalibrationSource.Manual, calibrated.CalibrationSource);
        Assert.Equal(0.5, calibrated.PixelSpacingXMm!.Value, precision: 6);
        Assert.Equal(0.5, calibrated.PixelSpacingYMm!.Value, precision: 6);
        Assert.True(calibrated.IsCalibrated);
    }

    [Fact]
    public async Task Unreadable_file_raises_decode_error_and_is_audited()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var imaging = provider.GetRequiredService<ImagingService>();

        var patient = await patients.CreateAsync(new PatientDraft { FirstName = "Nour", LastName = "Chatti" });
        var garbage = new byte[300];
        new Random(42).NextBytes(garbage);

        await Assert.ThrowsAsync<ImageDecodeException>(
            () => imaging.ImportAsync(patient.Id, new MemoryStream(garbage), "corrompu.dcm"));

        await using var db = await provider
            .GetRequiredService<IDbContextFactory<OrthoDbContext>>()
            .CreateDbContextAsync();
        Assert.Single(db.AuditEntries.Where(a => a.Action == "image.import.failed"));
    }

    /// <summary>
    /// Harnais du corpus réel (risque R1) : déposez des DICOM anonymisés de
    /// céphalostats tunisiens dans DicomCorpus/ et ce test les décode tous.
    /// </summary>
    [Fact]
    public async Task Real_dicom_corpus_decodes_when_present()
    {
        var corpusDirectory = Path.Combine(AppContext.BaseDirectory, "DicomCorpus");
        if (!Directory.Exists(corpusDirectory))
            return;

        var decoder = new MedicalImageDecoder();
        foreach (var file in Directory.EnumerateFiles(corpusDirectory, "*.dcm"))
        {
            var decoded = await decoder.DecodeAsync(await File.ReadAllBytesAsync(file), file);
            Assert.True(decoded.Width > 0 && decoded.Height > 0, $"Décodage vide pour {file}");
        }
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
