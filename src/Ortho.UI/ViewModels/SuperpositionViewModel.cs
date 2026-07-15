using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Domain.Cephalometry;
using Ortho.Domain.Entities;
using Ortho.UI.Localization;
using SkiaSharp;

namespace Ortho.UI.ViewModels;

public partial class SuperpositionViewModel(
    IReadOnlyList<MedicalImage> images, CephalometryService ceph, ImagingService imaging) : ViewModelBase
{
    /// <summary>Structures stables du recalage : ligne S-N (base du crâne).</summary>
    private static readonly string[] RegistrationCodes = ["S", "N"];

    public IReadOnlyList<MedicalImage> Images { get; } = images;
    public IReadOnlyList<AnalysisTemplate> Templates { get; } = AnalysisTemplates.All;

    [ObservableProperty] private AnalysisTemplate _selectedTemplate = AnalysisTemplates.Steiner;
    [ObservableProperty] private MedicalImage? _referenceImage;
    [ObservableProperty] private MedicalImage? _comparedImage;
    [ObservableProperty] private string _statusMessage = "";

    public SKBitmap? ReferenceBitmap { get; private set; }
    public IReadOnlyList<(ImagePoint, ImagePoint)> ReferenceSegments { get; private set; } = [];
    public IReadOnlyList<(ImagePoint, ImagePoint)> OverlaySegments { get; private set; } = [];

    public event Action? Updated;

    public string Title => L.Get("SuperpositionTitle");

    partial void OnSelectedTemplateChanged(AnalysisTemplate value) => _ = RefreshAsync();
    partial void OnReferenceImageChanged(MedicalImage? value) => _ = RefreshAsync();
    partial void OnComparedImageChanged(MedicalImage? value) => _ = RefreshAsync();

    public async Task RefreshAsync()
    {
        ReferenceSegments = [];
        OverlaySegments = [];
        ReferenceBitmap = null;

        if (ReferenceImage is null || ComparedImage is null)
        {
            StatusMessage = L.Get("SuperpositionSelectImages");
            Updated?.Invoke();
            return;
        }

        var reference = await LoadPointsAsync(ReferenceImage);
        var compared = await LoadPointsAsync(ComparedImage);

        if (reference is null || compared is null)
        {
            StatusMessage = L.F("SuperpositionMissing", SelectedTemplate.Name);
            Updated?.Invoke();
            return;
        }

        await using (var stream = await imaging.OpenDisplayAsync(ReferenceImage))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            ReferenceBitmap = SKBitmap.Decode(buffer.ToArray());
        }

        // Recalage : la similitude qui envoie (S,N) de T1 sur (S,N) de T0.
        var transform = SimilarityTransform.FromTwoPoints(
            compared["S"], compared["N"], reference["S"], reference["N"]);
        var transformed = compared.ToDictionary(kv => kv.Key, kv => transform.Apply(kv.Value));

        ReferenceSegments = CephTracing.BuildSegments(SelectedTemplate, reference);
        OverlaySegments = CephTracing.BuildSegments(SelectedTemplate, transformed);
        StatusMessage = L.Get("SuperpositionHint");
        Updated?.Invoke();
    }

    /// <summary>Landmarks de l'analyse si elle existe et contient les points de recalage, sinon null.</summary>
    private async Task<Dictionary<string, ImagePoint>?> LoadPointsAsync(MedicalImage image)
    {
        var analysis = await ceph.FindAsync(image.Id, SelectedTemplate.Code);
        if (analysis is null)
            return null;

        var points = analysis.Landmarks.ToDictionary(
            l => l.Code, l => new ImagePoint(l.X, l.Y));
        return RegistrationCodes.All(points.ContainsKey) ? points : null;
    }
}
