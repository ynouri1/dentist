using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;
using Ortho.UI.Controls;
using Ortho.UI.Localization;
using SkiaSharp;

namespace Ortho.UI.ViewModels;

public partial class ImageViewerViewModel(MedicalImage image, ImagingService imaging) : ViewModelBase
{
    public MedicalImage Image { get; } = image;
    public ObservableCollection<AnnotationDisplay> Annotations { get; } = [];

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _annotationText = "";

    public SKBitmap? Bitmap { get; private set; }

    /// <summary>Levé quand bitmap et annotations sont prêts à être poussés au contrôle.</summary>
    public event Action? Loaded;

    public string Title => L.F("ViewerTitle", Image.FileName);

    public string CalibrationLabel => Image switch
    {
        { IsCalibrated: false } => L.Get("MeasuresInPixels"),
        { CalibrationSource: CalibrationSource.Manual }
            => L.F("CalibrationManual", Image.PixelSpacingXMm!.Value.ToString("F4", CultureInfo.CurrentCulture)),
        _ => L.F("CalibrationDicom", Image.PixelSpacingXMm!.Value.ToString("F4", CultureInfo.CurrentCulture)),
    };

    public async Task LoadAsync()
    {
        await using (var stream = await imaging.OpenDisplayAsync(Image))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Bitmap = SKBitmap.Decode(buffer.ToArray());
        }

        var fresh = await imaging.GetImageAsync(Image.Id);
        Annotations.Clear();
        foreach (var annotation in fresh?.Annotations ?? [])
            Annotations.Add(ToDisplay(annotation));

        Loaded?.Invoke();
    }

    public async Task AddAnnotationAsync(AnnotationType type, IReadOnlyList<ImagePoint> points)
    {
        if (type == AnnotationType.Text && string.IsNullOrWhiteSpace(AnnotationText))
        {
            StatusMessage = L.Get("AnnotationTextRequired");
            return;
        }

        try
        {
            var text = type == AnnotationType.Text ? AnnotationText : null;
            var entity = await imaging.AddAnnotationAsync(Image.Id, type, points, text);
            Annotations.Add(ToDisplay(entity));
            StatusMessage = "";
        }
        catch (ValidationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteLastAsync()
    {
        if (Annotations.Count == 0)
            return;

        var last = Annotations[^1];
        await imaging.DeleteAnnotationAsync(last.Id);
        Annotations.Remove(last);
    }

    private AnnotationDisplay ToDisplay(ImageAnnotation annotation)
    {
        var points = ImagingService.ParsePoints(annotation.PointsJson);
        return new AnnotationDisplay(annotation.Id, annotation.Type, points, ComputeLabel(annotation, points));
    }

    private string? ComputeLabel(ImageAnnotation annotation, IReadOnlyList<ImagePoint> points)
    {
        switch (annotation.Type)
        {
            case AnnotationType.Distance when points.Count == 2:
                if (Image.IsCalibrated)
                {
                    var mm = GeometryCalculator.Distance(
                        points[0], points[1], Image.PixelSpacingXMm!.Value, Image.PixelSpacingYMm!.Value);
                    return $"{mm.ToString("F1", CultureInfo.CurrentCulture)} mm";
                }
                var px = GeometryCalculator.Distance(points[0], points[1]);
                return $"{px.ToString("F0", CultureInfo.CurrentCulture)} px";

            case AnnotationType.Angle when points.Count == 3:
                var degrees = GeometryCalculator.AngleDegrees(
                    points[0], points[1], points[2],
                    Image.PixelSpacingXMm ?? 1, Image.PixelSpacingYMm ?? 1);
                return $"{degrees.ToString("F1", CultureInfo.CurrentCulture)}°";

            case AnnotationType.Text:
                return annotation.Text;

            default:
                return null;
        }
    }
}
