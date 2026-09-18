using System.Globalization;
using Attendance.Core.Domain;
using ClosedXML.Excel;

namespace Attendance.Core.Export;

public sealed class PayrollExcelExporter
{
    public void Export(WeeklyReport report, string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Nomina");
        sheet.Cell(1, 1).Value = "Reporte semanal de asistencia";
        sheet.Cell(2, 1).Value = $"Semana {report.WeekStart:yyyy-MM-dd} a {report.WeekEnd:yyyy-MM-dd}";
        sheet.Range(1, 1, 1, 5).Merge();

        var headers = new[] { "Empleado", "Rol", "Horas", "Valor hora", "Pago" };
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
            sheet.Cell(row, 4).Value = line.HourlyRate;
            sheet.Cell(row, 5).Value = line.Pay;
            row++;
        }

        sheet.Cell(row, 1).Value = "Total";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalHours;
        sheet.Cell(row, 5).Value = report.TotalPay;
        sheet.Columns().AdjustToContents();

        var issues = workbook.Worksheets.Add("Anomalias");
        issues.Cell(1, 1).Value = "EmpleadoId";
        issues.Cell(1, 2).Value = "Fecha";
        issues.Cell(1, 3).Value = "Problema";
        issues.Row(1).Style.Font.Bold = true;
        var issueRow = 2;
        foreach (var issue in report.Issues)
        {
            issues.Cell(issueRow, 1).Value = issue.EmployeeId;
            issues.Cell(issueRow, 2).Value = issue.PunchedAt.ToString("g", CultureInfo.CurrentCulture);
            issues.Cell(issueRow, 3).Value = issue.Message;
            issueRow++;
        }

        issues.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }
}
