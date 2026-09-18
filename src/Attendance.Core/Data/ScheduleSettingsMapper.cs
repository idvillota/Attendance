using System.Globalization;
using Attendance.Core.Domain;

namespace Attendance.Core.Data;

internal static class ScheduleSettingsMapper
{
    public static TenantSchedule FromMap(IReadOnlyDictionary<string, string> map)
    {
        var schedule = TenantSchedule.Default();
        if (TimeOnly.TryParse(SqliteSession.GetString(map, "night_start", "19:00"), out var nightStart))
        {
            schedule.NightStart = nightStart;
        }

        if (TimeOnly.TryParse(SqliteSession.GetString(map, "night_end", "06:00"), out var nightEnd))
        {
            schedule.NightEnd = nightEnd;
        }

        if (decimal.TryParse(SqliteSession.GetString(map, "extra_after_hours", "8"), CultureInfo.InvariantCulture, out var extra))
        {
            schedule.ExtraAfterHours = extra;
        }

        var cells = SqliteSession.GetString(map, "schedule_cells", "");
        if (!string.IsNullOrWhiteSpace(cells))
        {
            var parsed = cells.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v =>
                {
                    var number = int.Parse(v, CultureInfo.InvariantCulture);
                    return Enum.IsDefined(typeof(CalendarBand), number)
                        ? (CalendarBand)number
                        : CalendarBand.OrdinaryDay;
                })
                .ToArray();
            if (parsed.Length == TenantSchedule.CellCount)
            {
                schedule.Cells = parsed;
            }
        }
        else
        {
            schedule.PaintFromNightWindow();
        }

        var holidays = SqliteSession.GetString(map, "holidays", "");
        if (!string.IsNullOrWhiteSpace(holidays))
        {
            foreach (var token in holidays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (DateOnly.TryParse(token, out var date))
                {
                    schedule.Holidays.Add(date);
                }
            }
        }

        return schedule;
    }

    public static IReadOnlyDictionary<string, string> ToMap(TenantSchedule schedule) => new Dictionary<string, string>
    {
        ["night_start"] = schedule.NightStart.ToString("HH:mm", CultureInfo.InvariantCulture),
        ["night_end"] = schedule.NightEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
        ["extra_after_hours"] = schedule.ExtraAfterHours.ToString(CultureInfo.InvariantCulture),
        ["schedule_cells"] = string.Join(",", schedule.Cells.Select(c => ((int)c).ToString(CultureInfo.InvariantCulture))),
        ["holidays"] = string.Join(",", schedule.Holidays.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
    };
}
