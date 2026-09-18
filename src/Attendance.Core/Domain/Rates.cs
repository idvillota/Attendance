using System.Text.Json;

namespace Attendance.Core.Domain;

public enum CalendarBand
{
    OrdinaryDay = 0,
    OrdinaryNight = 1,
    SundayDay = 2,
    SundayNight = 3
}

public enum RateBand
{
    OrdinaryDay,
    OrdinaryNight,
    SundayDay,
    SundayNight,
    ExtraOrdinaryDay,
    ExtraOrdinaryNight,
    ExtraSundayDay,
    ExtraSundayNight
}

public sealed class RoleRates
{
    public decimal OrdinaryDay { get; set; }
    public decimal OrdinaryNight { get; set; }
    public decimal SundayDay { get; set; }
    public decimal SundayNight { get; set; }
    public decimal ExtraOrdinaryDay { get; set; }
    public decimal ExtraOrdinaryNight { get; set; }
    public decimal ExtraSundayDay { get; set; }
    public decimal ExtraSundayNight { get; set; }

    public decimal Get(RateBand band) => band switch
    {
        RateBand.OrdinaryDay => OrdinaryDay,
        RateBand.OrdinaryNight => OrdinaryNight,
        RateBand.SundayDay => SundayDay,
        RateBand.SundayNight => SundayNight,
        RateBand.ExtraOrdinaryDay => ExtraOrdinaryDay,
        RateBand.ExtraOrdinaryNight => ExtraOrdinaryNight,
        RateBand.ExtraSundayDay => ExtraSundayDay,
        RateBand.ExtraSundayNight => ExtraSundayNight,
        _ => OrdinaryDay
    };

    public static RateBand ToPaidBand(CalendarBand calendar, bool extra) => (calendar, extra) switch
    {
        (CalendarBand.OrdinaryDay, false) => RateBand.OrdinaryDay,
        (CalendarBand.OrdinaryNight, false) => RateBand.OrdinaryNight,
        (CalendarBand.SundayDay, false) => RateBand.SundayDay,
        (CalendarBand.SundayNight, false) => RateBand.SundayNight,
        (CalendarBand.OrdinaryDay, true) => RateBand.ExtraOrdinaryDay,
        (CalendarBand.OrdinaryNight, true) => RateBand.ExtraOrdinaryNight,
        (CalendarBand.SundayDay, true) => RateBand.ExtraSundayDay,
        (CalendarBand.SundayNight, true) => RateBand.ExtraSundayNight,
        _ => RateBand.OrdinaryDay
    };

    public static RoleRates FromOrdinary(decimal ordinary)
    {
        decimal R(decimal factor) => decimal.Round(ordinary * factor, 0, MidpointRounding.AwayFromZero);
        return new RoleRates
        {
            OrdinaryDay = decimal.Round(ordinary, 0, MidpointRounding.AwayFromZero),
            OrdinaryNight = R(1.35m),
            SundayDay = R(1.90m),
            SundayNight = R(2.25m),
            ExtraOrdinaryDay = R(1.25m),
            ExtraOrdinaryNight = R(1.75m),
            ExtraSundayDay = R(2.15m),
            ExtraSundayNight = R(2.65m)
        };
    }

    public static RoleRates LegalColombia2026() => new()
    {
        OrdinaryDay = 8338m,
        OrdinaryNight = 11256m,
        SundayDay = 15842m,
        SundayNight = 18760m,
        ExtraOrdinaryDay = 10422m,
        ExtraOrdinaryNight = 14591m,
        ExtraSundayDay = 17927m,
        ExtraSundayNight = 22095m
    };

    public string ToJson() => JsonSerializer.Serialize(this);

    public static RoleRates FromJson(string? json, decimal fallbackOrdinary)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return FromOrdinary(fallbackOrdinary);
        }

        try
        {
            return JsonSerializer.Deserialize<RoleRates>(json) ?? FromOrdinary(fallbackOrdinary);
        }
        catch (JsonException)
        {
            return FromOrdinary(fallbackOrdinary);
        }
    }
}

public sealed class TenantSchedule
{
    public const int Days = 7;
    public const int Hours = 24;
    public const int CellCount = Days * Hours;

    public TimeOnly NightStart { get; set; } = new(19, 0);
    public TimeOnly NightEnd { get; set; } = new(6, 0);
    public decimal ExtraAfterHours { get; set; } = 8m;
    public CalendarBand[] Cells { get; set; } = new CalendarBand[CellCount];
    public HashSet<DateOnly> Holidays { get; set; } = [];

    public static TenantSchedule Default()
    {
        var schedule = new TenantSchedule();
        schedule.PaintFromNightWindow();
        return schedule;
    }

    public CalendarBand BandAt(DateTime at)
    {
        var date = DateOnly.FromDateTime(at);
        var dow = Holidays.Contains(date) ? DayOfWeek.Sunday : at.DayOfWeek;
        return Cell(dow, at.Hour);
    }

    public CalendarBand Cell(DayOfWeek dow, int hour) => Cells[Index(dow, hour)];

    public void SetCell(DayOfWeek dow, int hour, CalendarBand band) => Cells[Index(dow, hour)] = band;

    public static int Index(DayOfWeek dow, int hour)
    {
        var mondayFirst = ((int)dow - (int)DayOfWeek.Monday + 7) % 7;
        return mondayFirst * Hours + hour;
    }

    public void PaintFromNightWindow()
    {
        Cells = new CalendarBand[CellCount];
        for (var day = 0; day < Days; day++)
        {
            var dow = (DayOfWeek)(((int)DayOfWeek.Monday + day) % 7);
            var sunday = dow == DayOfWeek.Sunday;
            for (var hour = 0; hour < Hours; hour++)
            {
                var night = IsNightHour(hour);
                Cells[day * Hours + hour] = (sunday, night) switch
                {
                    (true, true) => CalendarBand.SundayNight,
                    (true, false) => CalendarBand.SundayDay,
                    (false, true) => CalendarBand.OrdinaryNight,
                    _ => CalendarBand.OrdinaryDay
                };
            }
        }
    }

    public bool IsNightHour(int hour)
    {
        var start = NightStart.Hour;
        var end = NightEnd.Hour;
        return start <= end
            ? hour >= start && hour < end
            : hour >= start || hour < end;
    }
}

public sealed class PaySlice
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public RateBand Band { get; init; }
    public decimal Hours { get; init; }
    public decimal Rate { get; init; }
    public decimal Pay { get; init; }
    public string BandLabel => Band switch
    {
        RateBand.OrdinaryDay => "Ordinaria diurna",
        RateBand.OrdinaryNight => "Ordinaria nocturna",
        RateBand.SundayDay => "Dominical diurna",
        RateBand.SundayNight => "Dominical nocturna",
        RateBand.ExtraOrdinaryDay => "Extra diurna",
        RateBand.ExtraOrdinaryNight => "Extra nocturna",
        RateBand.ExtraSundayDay => "Extra dominical diurna",
        RateBand.ExtraSundayNight => "Extra dominical nocturna",
        _ => Band.ToString()
    };
}
