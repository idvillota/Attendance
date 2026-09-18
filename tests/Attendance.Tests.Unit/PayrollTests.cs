using Attendance.Core.Domain;
using Attendance.Core.Payroll;
using static Attendance.Tests.Unit.PunchFactory;

namespace Attendance.Tests.Unit;

public class ShiftPairingTests
{
    [Fact]
    public void Pairs_in_and_out_into_hours()
    {
        var punches = new[]
        {
            MakePunch(1, "2026-09-14T08:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-14T16:00:00", PunchDirection.Out)
        };

        var result = ShiftPairing.Pair(punches);

        var shift = Assert.Single(result.Shifts);
        Assert.Equal(8m, shift.Hours);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Flags_missing_out()
    {
        var punches = new[]
        {
            MakePunch(1, "2026-09-14T08:00:00", PunchDirection.In)
        };

        var result = ShiftPairing.Pair(punches);

        Assert.Empty(result.Shifts);
        Assert.Equal("Entrada sin salida", Assert.Single(result.Issues).Message);
        Assert.Equal(punches[0].Id, Assert.Single(result.OpenIns).Id);
    }

    [Fact]
    public void Unknown_direction_alternates_in_out()
    {
        var punches = new[]
        {
            MakePunch(1, "2026-09-14T08:00:00", PunchDirection.Unknown),
            MakePunch(1, "2026-09-14T12:00:00", PunchDirection.Unknown)
        };

        var result = ShiftPairing.Pair(punches);

        Assert.Equal(4m, Assert.Single(result.Shifts).Hours);
        Assert.Empty(result.Issues);
    }
}

public class WeeklyPayrollServiceTests
{
    [Fact]
    public void Pay_uses_ordinary_day_rate_for_weekday_daytime()
    {
        var weekStart = new DateOnly(2026, 9, 14);
        var roles = new[]
        {
            new Role { Id = 1, Name = "Mesero", IsActive = true, Rates = RoleRates.FromOrdinary(8000m) }
        };
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", RoleId = 1, RoleName = "Mesero", DeviceUserId = "1", IsActive = true }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-14T08:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-14T16:00:00", PunchDirection.Out),
            MakePunch(1, "2026-09-15T08:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-15T16:00:00", PunchDirection.Out)
        };

        var report = new WeeklyPayrollService().Build(weekStart, employees, roles, punches, TenantSchedule.Default());

        var line = Assert.Single(report.Lines);
        Assert.Equal(16m, line.Hours);
        Assert.Equal(128000m, line.Pay);
        Assert.Equal(128000m, report.TotalPay);
    }

    [Fact]
    public void Sunday_night_hour_pays_more_than_monday_morning()
    {
        var weekStart = new DateOnly(2026, 9, 14);
        var rates = new RoleRates
        {
            OrdinaryDay = 10000m,
            OrdinaryNight = 14000m,
            SundayDay = 15000m,
            SundayNight = 18000m,
            ExtraOrdinaryDay = 10000m,
            ExtraOrdinaryNight = 14000m,
            ExtraSundayDay = 15000m,
            ExtraSundayNight = 18000m
        };
        var roles = new[] { new Role { Id = 1, Name = "Mesero", IsActive = true, Rates = rates } };
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", RoleId = 1, IsActive = true }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-14T09:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-14T10:00:00", PunchDirection.Out),
            MakePunch(1, "2026-09-20T20:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-20T21:00:00", PunchDirection.Out)
        };

        var report = new WeeklyPayrollService().Build(weekStart, employees, roles, punches, TenantSchedule.Default());
        var line = Assert.Single(report.Lines);
        Assert.Equal(2m, line.Hours);
        Assert.Equal(28000m, line.Pay);
    }

    [Fact]
    public void Ignores_punches_outside_the_week()
    {
        var weekStart = new DateOnly(2026, 9, 14);
        var roles = new[] { new Role { Id = 1, Name = "Mesero", IsActive = true, Rates = RoleRates.FromOrdinary(8000m) } };
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", RoleId = 1, IsActive = true }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-13T08:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-13T16:00:00", PunchDirection.Out)
        };

        var report = new WeeklyPayrollService().Build(weekStart, employees, roles, punches);

        Assert.Equal(0m, Assert.Single(report.Lines).Hours);
    }
}

public class WeekRangeTests
{
    [Fact]
    public void Monday_is_start_of_week()
    {
        Assert.Equal(new DateOnly(2026, 9, 14), WeekRange.StartOfWeek(new DateOnly(2026, 9, 17)));
    }
}

public class PresenceTests
{
    [Fact]
    public void Inactive_employee_is_grey_even_with_open_in()
    {
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", RoleName = "Mesero", IsActive = false }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-17T08:00:00", PunchDirection.In)
        };

        var status = Assert.Single(Presence.Snapshot(employees, punches));

        Assert.Equal(PresenceKind.Disabled, status.Kind);
        Assert.Equal("Inactivo", status.Label);
    }

    [Fact]
    public void Active_without_open_shift_is_yellow()
    {
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", RoleName = "Mesero", IsActive = true }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-17T08:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-17T16:00:00", PunchDirection.Out)
        };

        var status = Assert.Single(Presence.Snapshot(employees, punches));

        Assert.Equal(PresenceKind.NotCheckedIn, status.Kind);
        Assert.Equal("Fuera", status.Label);
    }

    [Fact]
    public void Active_with_open_in_is_green()
    {
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", RoleName = "Mesero", IsActive = true }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-17T08:00:00", PunchDirection.In)
        };

        var status = Assert.Single(Presence.Snapshot(employees, punches));

        Assert.Equal(PresenceKind.CheckedIn, status.Kind);
        Assert.Equal(DateTime.Parse("2026-09-17T08:00:00"), status.ClockedInAt);
    }

    [Fact]
    public void Closed_shift_after_abandoned_in_is_not_working()
    {
        var employees = new[]
        {
            new Employee { Id = 1, Name = "Ana", IsActive = true }
        };
        var punches = new[]
        {
            MakePunch(1, "2026-09-17T08:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-17T09:00:00", PunchDirection.In),
            MakePunch(1, "2026-09-17T16:00:00", PunchDirection.Out)
        };

        var status = Assert.Single(Presence.Snapshot(employees, punches));

        Assert.Equal(PresenceKind.NotCheckedIn, status.Kind);
    }
}

internal static class PunchFactory
{
    public static Punch MakePunch(long employeeId, string iso, PunchDirection direction) => new()
    {
        Id = employeeId + iso.GetHashCode(),
        EmployeeId = employeeId,
        PunchedAt = DateTime.Parse(iso),
        Direction = direction,
        Source = PunchSource.Mock
    };
}
