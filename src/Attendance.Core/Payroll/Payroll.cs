using Attendance.Core.Domain;

namespace Attendance.Core.Payroll;

public static class WeekRange
{
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    public static DateOnly EndOfWeek(DateOnly date) => StartOfWeek(date).AddDays(6);

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static DateTime RangeStart(DateOnly weekStart) => weekStart.ToDateTime(TimeOnly.MinValue);

    public static DateTime RangeEndExclusive(DateOnly weekStart) => weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue);
}

public static class ShiftPairing
{
    public sealed record Result(
        IReadOnlyList<Shift> Shifts,
        IReadOnlyList<PunchIssue> Issues,
        IReadOnlyList<Punch> OpenIns);

    public static Result Pair(IEnumerable<Punch> punches)
    {
        var shifts = new List<Shift>();
        var issues = new List<PunchIssue>();
        var openIns = new List<Punch>();

        foreach (var group in punches.GroupBy(p => p.EmployeeId))
        {
            Punch? open = null;
            foreach (var punch in group.OrderBy(p => p.PunchedAt).ThenBy(p => p.Id))
            {
                if (open is null)
                {
                    open = punch;
                    continue;
                }

                var hours = (decimal)(punch.PunchedAt - open.PunchedAt).TotalHours;
                if (hours < 0)
                {
                    hours = 0;
                }

                shifts.Add(new Shift(open.EmployeeId, open.PunchedAt, punch.PunchedAt, decimal.Round(hours, 2), open.Id, punch.Id));
                open = null;
            }

            if (open is not null)
            {
                issues.Add(new PunchIssue(open.EmployeeId, open.Id, open.PunchedAt, "Entrada sin salida"));
                openIns.Add(open);
            }
        }

        return new Result(shifts, issues, openIns);
    }
}

public sealed class WeeklyPayrollService : IWeeklyPayrollService
{
    public WeeklyReport Build(
        DateOnly weekStart,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Role> roles,
        IReadOnlyList<Punch> punches,
        TenantSchedule? schedule = null)
    {
        schedule ??= TenantSchedule.Default();
        var weekEnd = weekStart.AddDays(6);
        var from = WeekRange.RangeStart(weekStart);
        var to = WeekRange.RangeEndExclusive(weekStart);
        var weekPunches = punches
            .Where(p => p.PunchedAt >= from && p.PunchedAt < to)
            .ToList();

        var paired = ShiftPairing.Pair(weekPunches);
        var shiftsByEmployee = paired.Shifts.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var roleById = roles.ToDictionary(r => r.Id);
        var lines = new List<WeeklyReportLine>();

        foreach (var employee in employees.Where(e => e.IsActive).OrderBy(e => e.Name))
        {
            roleById.TryGetValue(employee.RoleId, out var role);
            var rates = role?.Rates ?? RoleRates.FromOrdinary(role?.HourlyRate ?? 0m);
            var shifts = shiftsByEmployee.GetValueOrDefault(employee.Id) ?? [];
            var slices = BandedPay.Price(shifts, schedule, rates);
            var hours = decimal.Round(slices.Sum(s => s.Hours), 2);
            var pay = slices.Sum(s => s.Pay);

            lines.Add(new WeeklyReportLine
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                RoleName = role?.Name ?? employee.RoleName,
                HourlyRate = rates.OrdinaryDay,
                Hours = hours,
                Pay = pay,
                Breakdown = BandedPay.Summarize(slices),
                Slices = slices
            });
        }

        var names = employees.ToDictionary(e => e.Id, e => e.Name);
        return new WeeklyReport
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Lines = lines,
            Issues = paired.Issues
                .Select(issue => issue with { EmployeeName = names.GetValueOrDefault(issue.EmployeeId, "") })
                .ToList(),
            Shifts = paired.Shifts
        };
    }
}
