using Attendance.Core.Domain;

namespace Attendance.Core.Export;

public interface IPayrollExporter
{
    void Export(WeeklyReport report, string path);
}
