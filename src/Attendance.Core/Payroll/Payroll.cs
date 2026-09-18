using Attendance.Core.Domain;

namespace Attendance.Core.Payroll;

public static class WeekRange
{
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static DateTime RangeStart(DateOnly weekStart) => weekStart.ToDateTime(TimeOnly.MinValue);

    public static DateTime RangeEndExclusive(DateOnly weekStart) => weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue);
}

public static class ShiftPairing
{
    public sealed record Result(IReadOnlyList<Shift> Shifts, IReadOnlyList<PunchIssue> Issues);

    public static Result Pair(IEnumerable<Punch> punches)
    {
        var shifts = new List<Shift>();
        var issues = new List<PunchIssue>();

        foreach (var group in punches.GroupBy(p => p.EmployeeId))
        {
            Punch? open = null;
            foreach (var punch in group.OrderBy(p => p.PunchedAt).ThenBy(p => p.Id))
            {
                var direction = punch.Direction == PunchDirection.Unknown
                    ? open is null ? PunchDirection.In : PunchDirection.Out
                    : punch.Direction;

                if (direction == PunchDirection.In)
                {
                    if (open is not null)
                    {
                        issues.Add(new PunchIssue(open.EmployeeId, open.Id, open.PunchedAt, "Entrada sin salida"));
                    }

                    open = punch;
                    continue;
                }

                if (open is null)
                {
                    issues.Add(new PunchIssue(punch.EmployeeId, punch.Id, punch.PunchedAt, "Salida sin entrada"));
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
            }
        }

        return new Result(shifts, issues);
    }
}

public sealed class WeeklyPayrollService
{
    public WeeklyReport Build(
        DateOnly weekStart,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Role> roles,
        IReadOnlyList<Punch> punches)
    {
        var weekEnd = weekStart.AddDays(6);
        var from = WeekRange.RangeStart(weekStart);
        var to = WeekRange.RangeEndExclusive(weekStart);
        var weekPunches = punches
            .Where(p => p.PunchedAt >= from && p.PunchedAt < to)
            .ToList();

        var paired = ShiftPairing.Pair(weekPunches);
        var hoursByEmployee = paired.Shifts
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Hours));

        var roleById = roles.ToDictionary(r => r.Id);
        var lines = new List<WeeklyReportLine>();

        foreach (var employee in employees.Where(e => e.IsActive).OrderBy(e => e.Name))
        {
            roleById.TryGetValue(employee.RoleId, out var role);
            var hours = hoursByEmployee.GetValueOrDefault(employee.Id);
            var rate = role?.HourlyRate ?? 0m;
            var pay = decimal.Round(hours * rate, 0, MidpointRounding.AwayFromZero);

            lines.Add(new WeeklyReportLine
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                RoleName = role?.Name ?? employee.RoleName,
                HourlyRate = rate,
                Hours = hours,
                Pay = pay
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
