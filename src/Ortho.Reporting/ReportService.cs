using System.Globalization;
using Ortho.Application.Abstractions;
using Ortho.Application.Cephalometry;
using Ortho.Application.Documents;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Cephalometry;
using Ortho.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Ortho.Reporting;

/// <summary>
/// Génère le rapport céphalométrique PDF et l'archive automatiquement dans le
/// dossier patient (catégorie Document, chiffré comme le reste).
/// </summary>
public class ReportService(
    PatientService patients,
    ImagingService imaging,
    CephalometryService ceph,
    DocumentService documents,
    ICurrentUser currentUser,
    IAuditTrail audit)
{
    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<(byte[] Pdf, PatientDocument Archived)> GenerateCephReportAsync(
        MedicalImage image, string templateCode, CancellationToken ct = default)
    {
        var template = AnalysisTemplates.Get(templateCode);
        var analysis = await ceph.FindAsync(image.Id, templateCode, ct);
        if (analysis is null || analysis.Landmarks.Count == 0)
            throw new ValidationException(["Placez des landmarks avant de générer le rapport."]);

        var patient = await patients.GetAsync(image.PatientId, ct)
            ?? throw new InvalidOperationException($"Patient introuvable : {image.PatientId}");

        var points = analysis.Landmarks.ToDictionary(
            l => l.Code, l => new ImagePoint(l.X, l.Y));
        var results = CephalometryService.ComputeResults(
            template, points, image.PixelSpacingXMm, image.PixelSpacingYMm);

        byte[] sourcePng;
        await using (var stream = await imaging.OpenDisplayAsync(image, ct))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            sourcePng = buffer.ToArray();
        }
        var annotated = TracingImageRenderer.Render(
            sourcePng, points, CephTracing.BuildSegments(template, points));

        var data = new CephReportData(
            PracticeName: "Cabinet d'orthodontie",
            PractitionerName: currentUser.User?.DisplayName ?? currentUser.Name,
            PatientName: patient.FullName,
            FileNumber: patient.FileNumber,
            BirthDateAndAge: FormatBirth(patient),
            AnalysisName: template.Name,
            AnalysisVersion: analysis.TemplateVersion,
            GeneratedAt: DateTime.Now,
            AnnotatedImagePng: annotated,
            Measures: BuildRows(results),
            InterpretationLines: BuildInterpretation(template, results),
            CalibrationNote: FormatCalibration(image));

        var pdf = new CephReportDocument(data).GeneratePdf();

        var fileName = $"rapport-{template.Code}-{DateTime.Now:yyyyMMdd-HHmm}.pdf";
        var archived = await documents.ImportAsync(
            patient.Id, new MemoryStream(pdf), fileName, DocumentCategory.Document, ct);
        await audit.RecordAsync("report.generate", nameof(CephAnalysis), analysis.Id.ToString(), fileName, ct);

        return (pdf, archived);
    }

    private static IReadOnlyList<ReportMeasureRow> BuildRows(IReadOnlyList<MeasureResult> results)
        => results
            .Where(r => !r.Hidden)
            .Select(r => new ReportMeasureRow(
                r.Name,
                r.Value is { } v ? $"{v.ToString("F1", Culture)} {r.Unit}" : "—",
                $"{r.NormMean.ToString(Culture)} ± {r.NormSd.ToString(Culture)} {r.Unit}",
                r.DeviationSd is { } d ? $"{(d >= 0 ? "+" : "")}{d.ToString("F1", Culture)} σ" : "",
                r.Status))
            .ToList();

    private static IReadOnlyList<string> BuildInterpretation(
        AnalysisTemplate template, IReadOnlyList<MeasureResult> results)
    {
        var lines = new List<string>();

        // Classe squelettique d'après ANB (Steiner).
        if (template.Code == "steiner" &&
            results.FirstOrDefault(r => r.Code == "ANB")?.Value is { } anb)
        {
            lines.Add(anb switch
            {
                < 0 => $"ANB à {anb.ToString("F1", Culture)}° : tendance Classe III squelettique.",
                <= 4 => $"ANB à {anb.ToString("F1", Culture)}° : rapport squelettique de Classe I.",
                _ => $"ANB à {anb.ToString("F1", Culture)}° : tendance Classe II squelettique.",
            });
        }

        foreach (var result in results.Where(r => !r.Hidden && r.Value is not null))
        {
            if (result.Status is MeasureStatus.Outside or MeasureStatus.Borderline)
            {
                var direction = result.Value > result.NormMean ? "augmenté" : "diminué";
                var severity = result.Status == MeasureStatus.Outside ? "hors norme" : "à la limite";
                lines.Add(
                    $"{result.Name} {direction} ({result.Value!.Value.ToString("F1", Culture)} {result.Unit}, " +
                    $"{(result.DeviationSd >= 0 ? "+" : "")}{result.DeviationSd!.Value.ToString("F1", Culture)} σ) : {severity}.");
            }
        }

        if (lines.Count == 0)
            lines.Add("Toutes les mesures calculées sont dans les normes.");

        return lines;
    }

    private static string? FormatBirth(Patient patient)
    {
        if (patient.BirthDate is not { } birth)
            return null;
        var age = patient.AgeAt(DateOnly.FromDateTime(DateTime.Today));
        return $"Né(e) le {birth.ToString("dd/MM/yyyy", Culture)} ({age} ans)";
    }

    private static string? FormatCalibration(MedicalImage image) => image switch
    {
        { IsCalibrated: true, CalibrationSource: CalibrationSource.Dicom } =>
            $"Calibration : {image.PixelSpacingXMm!.Value.ToString("F4", Culture)} mm/pixel (métadonnées DICOM).",
        { IsCalibrated: true } =>
            $"Calibration : {image.PixelSpacingXMm!.Value.ToString("F4", Culture)} mm/pixel (règle étalon).",
        _ => "Image non calibrée : les mesures en millimètres ne sont pas disponibles.",
    };

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");
}
