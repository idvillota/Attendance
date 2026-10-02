using Attendance.Core.Domain;

namespace Attendance.Core.Export;

public interface IPayrollPdfExporter
{
    void Export(WeeklyReport report, string path);
}
