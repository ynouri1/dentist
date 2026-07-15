using Ortho.Application.Cephalometry;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ortho.Reporting;

/// <summary>Rapport céphalométrique A4 : en-tête cabinet, image tracée, mesures, interprétation.</summary>
public class CephReportDocument(CephReportData data) : IDocument
{
    private static readonly string Green = "#2e9e5b";
    private static readonly string Orange = "#e08a00";
    private static readonly string Red = "#c0392b";
    private static readonly string Gray = "#7f8c8d";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Rapport céphalométrique — {data.PatientName}",
        Author = data.PracticeName,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(12).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.BorderBottom(1).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(data.PracticeName).FontSize(15).Bold();
                column.Item().Text(data.PractitionerName).FontColor(Gray);
            });
            row.ConstantItem(200).AlignRight().Column(column =>
            {
                column.Item().Text($"{data.AnalysisName}").FontSize(12).SemiBold();
                column.Item().Text($"Version du modèle : {data.AnalysisVersion}").FontSize(8).FontColor(Gray);
                column.Item().Text($"Édité le {data.GeneratedAt:dd/MM/yyyy à HH:mm}").FontSize(8).FontColor(Gray);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(12);

            // Identité patient
            column.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(data.PatientName).FontSize(13).Bold();
                    if (data.BirthDateAndAge is { } birth)
                        c.Item().Text(birth).FontColor(Gray);
                });
                row.ConstantItem(160).AlignRight().Text($"Dossier {data.FileNumber}").SemiBold();
            });

            // Image tracée
            column.Item().AlignCenter().MaxHeight(330).Image(data.AnnotatedImagePng).FitArea();

            // Tableau des mesures
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Mesure");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Valeur");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Norme");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Écart");

                    static IContainer HeaderCell(IContainer cell) => cell
                        .BorderBottom(1).PaddingVertical(4).DefaultTextStyle(s => s.SemiBold());
                });

                foreach (var measure in data.Measures)
                {
                    var color = measure.Status switch
                    {
                        MeasureStatus.Normal => Green,
                        MeasureStatus.Borderline => Orange,
                        MeasureStatus.Outside => Red,
                        _ => Gray,
                    };

                    table.Cell().Element(BodyCell).Text(measure.Name);
                    table.Cell().Element(BodyCell).AlignRight().Text(measure.Value).FontColor(color).SemiBold();
                    table.Cell().Element(BodyCell).AlignRight().Text(measure.Norm).FontColor(Gray);
                    table.Cell().Element(BodyCell).AlignRight().Text(measure.Deviation).FontColor(color);

                    static IContainer BodyCell(IContainer cell) => cell
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
                }
            });

            // Interprétation
            if (data.InterpretationLines.Count > 0)
            {
                column.Item().Column(c =>
                {
                    c.Item().Text("Interprétation").FontSize(11).SemiBold();
                    foreach (var line in data.InterpretationLines)
                        c.Item().PaddingLeft(6).Text($"• {line}");
                });
            }

            if (data.CalibrationNote is { } note)
                column.Item().Text(note).FontSize(8).Italic().FontColor(Gray);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.BorderTop(0.5f).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text(
                "Document généré par Ortho — les mesures assistées ne remplacent pas le jugement clinique.")
                .FontSize(7).FontColor(Gray);
            row.ConstantItem(60).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(7).FontColor(Gray));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }
}
