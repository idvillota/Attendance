using System.Globalization;
using Attendance.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Attendance.Core.Export;

public sealed class PayrollPdfExporter : IPayrollPdfExporter
{
    private static readonly CultureInfo Cop = CultureInfo.GetCultureInfo("es-CO");

    public void Export(WeeklyReport report, string path)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.Letter);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor(Colors.Grey.Darken3));
                page.Header().Column(header =>
                {
                    header.Item().Text("Host Asistencia").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                    header.Item().Text("Reporte semanal de nómina").FontSize(12);
                    header.Item().Text($"Semana {report.WeekStart:dd/MM/yyyy} a {report.WeekEnd:dd/MM/yyyy}");
                    header.Item().PaddingBottom(8);
                });

                page.Content().Column(body =>
                {
                    body.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1.4f);
                            columns.ConstantColumn(56);
                            columns.RelativeColumn(2.4f);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Empleado");
                            header.Cell().Element(HeaderCell).Text("Rol");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Horas");
                            header.Cell().Element(HeaderCell).Text("Desglose");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Pago");
                        });

                        foreach (var line in report.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(line.EmployeeName);
                            table.Cell().Element(BodyCell).Text(line.RoleName);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{line.Hours:0.##}");
                            table.Cell().Element(BodyCell).Text(line.Breakdown);
                            table.Cell().Element(BodyCell).AlignRight().Text(line.Pay.ToString("C0", Cop));
                        }

                        table.Cell().Element(TotalCell).Text("Total");
                        table.Cell().Element(TotalCell).Text(string.Empty);
                        table.Cell().Element(TotalCell).AlignRight().Text($"{report.TotalHours:0.##}");
                        table.Cell().Element(TotalCell).Text(string.Empty);
                        table.Cell().Element(TotalCell).AlignRight().Text(report.TotalPay.ToString("C0", Cop));
                    });

                    if (report.Issues.Count > 0)
                    {
                        body.Item().PaddingTop(16).Text("Anomalías (no se pagan hasta corregir)").SemiBold();
                        foreach (var issue in report.Issues)
                        {
                            body.Item().Text($"{issue.EmployeeName} · {issue.PunchedAt:g} · {issue.Message}");
                        }
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Horas × tarifa del rol (ordinaria / noche / domingo).  ");
                    text.Span("Página ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf(path);
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(4).DefaultTextStyle(text => text.SemiBold());

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

    private static IContainer TotalCell(IContainer container) =>
        container.Padding(4).DefaultTextStyle(text => text.SemiBold());
}
