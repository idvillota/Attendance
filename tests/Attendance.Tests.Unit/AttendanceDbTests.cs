using Attendance.Core.Data;
using Attendance.Core.Export;
using Attendance.Core.Payroll;

namespace Attendance.Tests.Unit;

public class AttendanceDbTests
{
    [Fact]
    public void Seeded_week_produces_payroll_and_excel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-test-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();

            var weekStart = WeekRange.StartOfWeek(WeekRange.Today());
            var report = new WeeklyPayrollService().Build(
                weekStart,
                db.GetEmployees(),
                db.GetRoles(),
                db.GetPunches());

            Assert.True(report.Lines.Count >= 5);

            var xlsx = Path.ChangeExtension(path, ".xlsx");
            new PayrollExcelExporter().Export(report, xlsx);
            Assert.True(File.Exists(xlsx));
            File.Delete(xlsx);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
