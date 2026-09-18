using System.Globalization;
using Attendance.Core.Domain;
using ClosedXML.Excel;

namespace Attendance.Core.Export;

public sealed class PayrollExcelExporter : IPayrollExporter
{
    public void Export(WeeklyReport report, string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Nomina");
        sheet.Cell(1, 1).Value = "Reporte semanal de asistencia";
        sheet.Cell(2, 1).Value = $"Semana {report.WeekStart:yyyy-MM-dd} a {report.WeekEnd:yyyy-MM-dd}";
        sheet.Range(1, 1, 1, 6).Merge();

        var headers = new[] { "Empleado", "Rol", "Horas", "Desglose", "Pago" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(4, i + 1).Value = headers[i];
            sheet.Cell(4, i + 1).Style.Font.Bold = true;
        }

        var row = 5;
        foreach (var line in report.Lines)
        {
            sheet.Cell(row, 1).Value = line.EmployeeName;
            sheet.Cell(row, 2).Value = line.RoleName;
            sheet.Cell(row, 3).Value = line.Hours;
            sheet.Cell(row, 4).Value = line.Breakdown;
            sheet.Cell(row, 5).Value = line.Pay;
            row++;
        }

        sheet.Cell(row, 1).Value = "Total";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalHours;
        sheet.Cell(row, 5).Value = report.TotalPay;
        sheet.Columns().AdjustToContents();

        var detail = workbook.Worksheets.Add("Por_hora");
        detail.Cell(1, 1).Value = "Empleado";
        detail.Cell(1, 2).Value = "Desde";
        detail.Cell(1, 3).Value = "Hasta";
        detail.Cell(1, 4).Value = "Tipo";
        detail.Cell(1, 5).Value = "Horas";
        detail.Cell(1, 6).Value = "Tarifa";
        detail.Cell(1, 7).Value = "Pago";
        detail.Row(1).Style.Font.Bold = true;
        var dRow = 2;
        foreach (var line in report.Lines)
        {
            foreach (var slice in line.Slices)
            {
                detail.Cell(dRow, 1).Value = line.EmployeeName;
                detail.Cell(dRow, 2).Value = slice.From;
                detail.Cell(dRow, 3).Value = slice.To;
                detail.Cell(dRow, 4).Value = slice.BandLabel;
                detail.Cell(dRow, 5).Value = slice.Hours;
                detail.Cell(dRow, 6).Value = slice.Rate;
                detail.Cell(dRow, 7).Value = slice.Pay;
                dRow++;
            }
        }

        detail.Columns().AdjustToContents();

        var issues = workbook.Worksheets.Add("Anomalias");
        issues.Cell(1, 1).Value = "Empleado";
        issues.Cell(1, 2).Value = "Fecha";
        issues.Cell(1, 3).Value = "Problema";
        issues.Row(1).Style.Font.Bold = true;
        var issueRow = 2;
        foreach (var issue in report.Issues)
        {
            issues.Cell(issueRow, 1).Value = issue.EmployeeName;
            issues.Cell(issueRow, 2).Value = issue.PunchedAt.ToString("g", CultureInfo.CurrentCulture);
            issues.Cell(issueRow, 3).Value = issue.Message;
            issueRow++;
        }

        issues.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }
}
