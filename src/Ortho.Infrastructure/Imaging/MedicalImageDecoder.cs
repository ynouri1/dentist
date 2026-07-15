using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.NativeCodec;
using Ortho.Application.Abstractions;
using Serilog;
using SixLabors.ImageSharp;

namespace Ortho.Infrastructure.Imaging;

public class MedicalImageDecoder : IMedicalImageDecoder
{
    static MedicalImageDecoder()
    {
        new DicomSetupBuilder()
            .RegisterServices(s => s
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>()
                .AddImageManager<ImageSharpImageManager>())
            .SkipValidation()
            .Build();
    }

    public Task<DecodedImage> DecodeAsync(byte[] content, string fileName, CancellationToken ct = default)
        => Task.Run(() => IsDicom(content) ? DecodeDicom(content) : DecodeRaster(content, fileName), ct);

    /// <summary>Préambule DICOM : 128 octets puis la signature « DICM ».</summary>
    public static bool IsDicom(ReadOnlySpan<byte> content)
        => content.Length > 132
           && content[128] == 'D' && content[129] == 'I' && content[130] == 'C' && content[131] == 'M';

    private static DecodedImage DecodeDicom(byte[] content)
    {
        try
        {
            var file = DicomFile.Open(new MemoryStream(content), FileReadOption.ReadAll);
            var dataset = file.Dataset;

            var dicomImage = new DicomImage(dataset);
            using var rendered = dicomImage.RenderImage().AsSharpImage();
            using var png = new MemoryStream();
            rendered.SaveAsPng(png);

            // PixelSpacing (calibré) prioritaire sur ImagerPixelSpacing (au détecteur).
            double? spacingX = null, spacingY = null;
            if (TryGetSpacing(dataset, DicomTag.PixelSpacing, out var s) ||
                TryGetSpacing(dataset, DicomTag.ImagerPixelSpacing, out s))
            {
                spacingY = s.RowMm;
                spacingX = s.ColumnMm;
            }

            DateTime? acquiredAt = null;
            if (dataset.TryGetSingleValue(DicomTag.AcquisitionDate, out DateTime acquisition))
                acquiredAt = acquisition;
            else if (dataset.TryGetSingleValue(DicomTag.StudyDate, out DateTime study))
                acquiredAt = study;

            return new DecodedImage(
                png.ToArray(),
                rendered.Width,
                rendered.Height,
                spacingX,
                spacingY,
                dataset.GetSingleValueOrDefault(DicomTag.Modality, (string?)null),
                dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, (string?)null),
                acquiredAt);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Décodage DICOM impossible");
            throw new ImageDecodeException($"Fichier DICOM illisible ({ex.Message}).", ex);
        }
    }

    private static bool TryGetSpacing(DicomDataset dataset, DicomTag tag, out (double RowMm, double ColumnMm) spacing)
    {
        spacing = default;
        if (!dataset.TryGetValues(tag, out double[]? values) || values is not { Length: >= 2 })
            return false;
        if (values[0] <= 0 || values[1] <= 0)
            return false;
        spacing = (values[0], values[1]);
        return true;
    }

    private static DecodedImage DecodeRaster(byte[] content, string fileName)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(content);
            using var png = new MemoryStream();
            image.SaveAsPng(png);

            return new DecodedImage(
                png.ToArray(), image.Width, image.Height,
                PixelSpacingXMm: null, PixelSpacingYMm: null,
                Modality: null, SourceDescription: null, AcquiredAt: null);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Décodage raster impossible pour {FileName}", fileName);
            throw new ImageDecodeException($"Image illisible ({ex.Message}).", ex);
        }
    }
}
