using Ortho.Application.Cephalometry;

namespace Ortho.Reporting;

/// <summary>Une ligne du tableau de mesures du rapport.</summary>
public record ReportMeasureRow(
    string Name,
    string Value,
    string Norm,
    string Deviation,
    MeasureStatus Status);

/// <summary>Tout ce qu'il faut pour composer le rapport, sans dépendance à la base.</summary>
public record CephReportData(
    string PracticeName,
    string PractitionerName,
    string PatientName,
    string FileNumber,
    string? BirthDateAndAge,
    string AnalysisName,
    string AnalysisVersion,
    DateTime GeneratedAt,
    byte[] AnnotatedImagePng,
    IReadOnlyList<ReportMeasureRow> Measures,
    IReadOnlyList<string> InterpretationLines,
    string? CalibrationNote);
