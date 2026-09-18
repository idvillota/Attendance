using Attendance.Core.Domain;

namespace Attendance.Core.Payroll;

public static class BandedPay
{
    public static IReadOnlyList<PaySlice> Price(
        IEnumerable<Shift> shifts,
        TenantSchedule schedule,
        RoleRates rates)
    {
        var slices = new List<PaySlice>();
        var workedByDate = new Dictionary<DateOnly, decimal>();

        foreach (var shift in shifts.OrderBy(s => s.ClockIn))
        {
            var cursor = shift.ClockIn;
            while (cursor < shift.ClockOut)
            {
                var nextHour = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0).AddHours(1);
                var sliceEnd = nextHour < shift.ClockOut ? nextHour : shift.ClockOut;
                var date = DateOnly.FromDateTime(cursor);
                var hours = (decimal)(sliceEnd - cursor).TotalHours;
                if (hours <= 0)
                {
                    cursor = sliceEnd;
                    continue;
                }

                var already = workedByDate.GetValueOrDefault(date);
                var extraAfter = schedule.ExtraAfterHours;
                if (extraAfter > 0 && already < extraAfter && already + hours > extraAfter)
                {
                    var before = extraAfter - already;
                    var split = cursor.AddTicks((long)(before * TimeSpan.TicksPerHour));
                    AddSlice(slices, workedByDate, schedule, rates, cursor, split, extra: false);
                    cursor = split;
                    continue;
                }

                var extra = extraAfter > 0 && already >= extraAfter;
                AddSlice(slices, workedByDate, schedule, rates, cursor, sliceEnd, extra);
                cursor = sliceEnd;
            }
        }

        return slices;
    }

    private static void AddSlice(
        List<PaySlice> slices,
        Dictionary<DateOnly, decimal> workedByDate,
        TenantSchedule schedule,
        RoleRates rates,
        DateTime from,
        DateTime to,
        bool extra)
    {
        var hours = (decimal)(to - from).TotalHours;
        if (hours <= 0)
        {
            return;
        }

        var calendar = schedule.BandAt(from);
        var band = RoleRates.ToPaidBand(calendar, extra);
        var rate = rates.Get(band);
        var pay = decimal.Round(hours * rate, 0, MidpointRounding.AwayFromZero);
        slices.Add(new PaySlice
        {
            From = from,
            To = to,
            Band = band,
            Hours = decimal.Round(hours, 4),
            Rate = rate,
            Pay = pay
        });

        var date = DateOnly.FromDateTime(from);
        workedByDate[date] = workedByDate.GetValueOrDefault(date) + hours;
    }

    public static string Summarize(IEnumerable<PaySlice> slices)
    {
        var parts = slices
            .GroupBy(s => s.BandLabel)
            .Select(g => $"{g.Sum(x => x.Hours):0.##}h {g.Key.ToLowerInvariant()}")
            .ToList();
        return parts.Count == 0 ? "Sin horas pagadas" : string.Join(" · ", parts);
    }
}
