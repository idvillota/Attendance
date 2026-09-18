using Attendance.Core.Domain;

namespace Attendance.Core.Payroll;

public interface IWeeklyPayrollService
{
    WeeklyReport Build(
        DateOnly weekStart,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Role> roles,
        IReadOnlyList<Punch> punches,
        TenantSchedule? schedule = null);
}
