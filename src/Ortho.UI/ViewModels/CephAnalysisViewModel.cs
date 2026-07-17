using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Cephalometry;
using Ortho.Domain.Entities;
using Ortho.UI.Controls;
using Ortho.UI.Localization;
using SkiaSharp;

namespace Ortho.UI.ViewModels;

public partial class LandmarkItemViewModel(LandmarkDefinition definition) : ViewModelBase
{
    public string Code => definition.Code;
    public string Name => definition.Name;

    [ObservableProperty] private bool _isPlaced;
    [ObservableProperty] private bool _isCurrent;

    public string Display => $"{Code} — {Name}";
}

public record MeasureRow(string Name, string Value, string Norm, IBrush StatusBrush, string DeviationText);

public partial class CephAnalysisViewModel(
    MedicalImage image, CephalometryService ceph, ImagingService imaging,
    Ortho.Reporting.ReportService reports, Ortho.Application.Abstractions.ILandmarkDetector detector)
    : ViewModelBase
{
    private CephAnalysis? _analysis;
    private readonly Dictionary<string, ImagePoint> _points = [];
    private readonly Stack<(string Code, ImagePoint? Previous)> _undo = [];
    private readonly Stack<(string Code, ImagePoint? Previous)> _redo = [];

    public MedicalImage Image { get; } = image;
    public SKBitmap? Bitmap { get; private set; }

    public IReadOnlyList<AnalysisTemplate> Templates => AnalysisTemplates.All;
    [ObservableProperty] private AnalysisTemplate _selectedTemplate = AnalysisTemplates.Steiner;

    public ObservableCollection<LandmarkItemViewModel> Landmarks { get; } = [];
    public ObservableCollection<MeasureRow> Results { get; } = [];

    [ObservableProperty] private LandmarkItemViewModel? _currentLandmark;
    [ObservableProperty] private string _statusMessage = "";

    /// <summary>Levé quand l'état (points, tracé, résultats) doit être repoussé au canvas.</summary>
    public event Action? StateChanged;

    public string Title => L.F("CephWindowTitle", Image.FileName);

    /// <summary>Vrai si un modèle IA est chargé : conditionne l'affichage du bouton de pré-placement.</summary>
    public bool AiAvailable => detector.IsAvailable;

    public async Task LoadAsync()
    {
        if (Bitmap is null)
        {
            await using var stream = await imaging.OpenDisplayAsync(Image);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Bitmap = SKBitmap.Decode(buffer.ToArray());
        }

        _analysis = await ceph.GetOrCreateAsync(Image.Id, SelectedTemplate.Code);

        _points.Clear();
        foreach (var landmark in _analysis.Landmarks)
            _points[landmark.Code] = new ImagePoint(landmark.X, landmark.Y);
        _undo.Clear();
        _redo.Clear();

        Landmarks.Clear();
        foreach (var code in SelectedTemplate.RequiredLandmarks)
            Landmarks.Add(new LandmarkItemViewModel(LandmarkCatalog.Get(code)));

        RefreshState();
    }

    partial void OnSelectedTemplateChanged(AnalysisTemplate value) => _ = LoadAsync();

    public async Task PlaceAsync(ImagePoint point)
    {
        if (_analysis is null || CurrentLandmark is not { } current)
            return;

        _undo.Push((current.Code, _points.TryGetValue(current.Code, out var old) ? old : null));
        _redo.Clear();
        _points[current.Code] = point;
        await ceph.SetLandmarkAsync(_analysis.Id, current.Code, point);
        RefreshState();
    }

    public async Task MoveAsync(string code, ImagePoint point)
    {
        if (_analysis is null)
            return;

        // Un déplacement continu (drag) n'empile qu'une seule entrée d'undo.
        if (_undo.Count == 0 || _undo.Peek().Code != code)
        {
            _undo.Push((code, _points.TryGetValue(code, out var old) ? old : null));
            _redo.Clear();
        }
        _points[code] = point;
        await ceph.SetLandmarkAsync(_analysis.Id, code, point);
        RefreshState();
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (_analysis is null || _undo.Count == 0)
            return;

        var (code, previous) = _undo.Pop();
        _redo.Push((code, _points.TryGetValue(code, out var current) ? current : null));

        if (previous is { } p)
        {
            _points[code] = p;
            await ceph.SetLandmarkAsync(_analysis.Id, code, p);
        }
        else
        {
            _points.Remove(code);
            await ceph.RemoveLandmarkAsync(_analysis.Id, code);
        }
        RefreshState();
    }

    [RelayCommand]
    private async Task RedoAsync()
    {
        if (_analysis is null || _redo.Count == 0)
            return;

        var (code, next) = _redo.Pop();
        _undo.Push((code, _points.TryGetValue(code, out var current) ? current : null));

        if (next is { } p)
        {
            _points[code] = p;
            await ceph.SetLandmarkAsync(_analysis.Id, code, p);
        }
        else
        {
            _points.Remove(code);
            await ceph.RemoveLandmarkAsync(_analysis.Id, code);
        }
        RefreshState();
    }

    /// <summary>
    /// Pré-placement IA : propose une position pour les points non encore placés.
    /// Le praticien doit valider/ajuster chaque point (l'IA ne décide pas seule).
    /// </summary>
    [RelayCommand]
    private async Task PreplaceWithAiAsync()
    {
        if (_analysis is null || !detector.IsAvailable || Bitmap is null)
            return;

        try
        {
            using var buffer = new MemoryStream();
            using (var data = Bitmap.Encode(SKEncodedImageFormat.Png, 100))
                data.SaveTo(buffer);

            var missing = Landmarks.Where(l => !l.IsPlaced).Select(l => l.Code).ToList();
            var detected = await detector.DetectAsync(buffer.ToArray(), missing);
            if (detected.Count == 0)
            {
                StatusMessage = L.Get("AiNoProposal");
                return;
            }

            await ceph.ApplyDetectedLandmarksAsync(_analysis.Id, detected);
            foreach (var landmark in detected)
                _points[landmark.Code] = landmark.Point;

            RefreshState();
            StatusMessage = L.F("AiPreplaced", detected.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = L.F("AiFailed", ex.Message);
        }
    }

    /// <summary>Génère le rapport PDF : archivé dans le dossier patient, copie exportée si chemin fourni.</summary>
    public async Task GenerateReportAsync(string? exportPath)
    {
        try
        {
            var (pdf, archived) = await reports.GenerateCephReportAsync(Image, SelectedTemplate.Code);
            if (exportPath is not null)
            {
                await File.WriteAllBytesAsync(exportPath, pdf);
                StatusMessage = L.F("ReportExported", exportPath);
            }
            else
            {
                StatusMessage = L.F("ReportArchived", archived.FileName);
            }
        }
        catch (ValidationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void SelectLandmark(LandmarkItemViewModel item) => SetCurrent(item);

    private void SetCurrent(LandmarkItemViewModel? item)
    {
        foreach (var landmark in Landmarks)
            landmark.IsCurrent = landmark == item;
        CurrentLandmark = item;
        StateChanged?.Invoke();
    }

    private void RefreshState()
    {
        foreach (var landmark in Landmarks)
            landmark.IsPlaced = _points.ContainsKey(landmark.Code);

        // Point courant : le prochain non placé (sauf sélection manuelle en cours de replacement).
        var current = CurrentLandmark is { IsPlaced: false } manual
            ? manual
            : Landmarks.FirstOrDefault(l => !l.IsPlaced);
        SetCurrent(current);

        RecomputeResults();
        StatusMessage = current is null
            ? L.Get("CephComplete")
            : L.F("CephPlacePrompt", current.Code, current.Name);
    }

    private void RecomputeResults()
    {
        var computed = CephalometryService.ComputeResults(
            SelectedTemplate, _points, Image.PixelSpacingXMm, Image.PixelSpacingYMm);

        Results.Clear();
        foreach (var result in computed)
        {
            if (result.Hidden)
                continue;
            var value = result.Value is { } v
                ? $"{v.ToString("F1", CultureInfo.CurrentCulture)} {result.Unit}"
                : "—";
            var deviation = result.DeviationSd is { } d
                ? $"{(d >= 0 ? "+" : "")}{d.ToString("F1", CultureInfo.CurrentCulture)} σ"
                : "";
            Results.Add(new MeasureRow(
                result.Name,
                value,
                $"{result.NormMean.ToString(CultureInfo.CurrentCulture)} ± {result.NormSd.ToString(CultureInfo.CurrentCulture)} {result.Unit}",
                StatusBrush(result.Status),
                deviation));
        }
    }

    private static IBrush StatusBrush(MeasureStatus status) => status switch
    {
        MeasureStatus.Normal => Brushes.MediumSeaGreen,
        MeasureStatus.Borderline => Brushes.Orange,
        MeasureStatus.Outside => Brushes.IndianRed,
        _ => Brushes.Gray,
    };

    public IReadOnlyList<PlacedLandmark> GetPlacedLandmarks()
        => _points
            .Select(kv => new PlacedLandmark(kv.Key, kv.Value, CurrentLandmark?.Code == kv.Key))
            .ToList();

    /// <summary>Segments du tracé : les lignes de chaque mesure dont les points sont placés.</summary>
    public IReadOnlyList<(ImagePoint, ImagePoint)> GetTraceLines()
        => CephTracing.BuildSegments(SelectedTemplate, _points);

    public bool HasCurrentLandmark => CurrentLandmark is not null;
}
