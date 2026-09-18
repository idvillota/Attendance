namespace Attendance.Core.Domain;

public enum PunchDirection
{
    In,
    Out,
    Unknown
}

public enum PunchSource
{
    Mock,
    Device,
    Manual,
    Import
}

public sealed class Role
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string RateLabel => $"{HourlyRate:N0} / hora";
    public string StatusLabel => IsActive ? "Activo" : "Inactivo";
}

public sealed class Employee
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string DeviceUserId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Subtitle =>
        $"{RoleName} · ID en el K30 {DeviceUserId}" + (IsActive ? string.Empty : " · Inactivo");
}

public sealed class Punch
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime PunchedAt { get; set; }
    public PunchDirection Direction { get; set; }
    public PunchSource Source { get; set; }
    public string? DeviceKey { get; set; }

    public string DirectionLabel => Direction switch
    {
        PunchDirection.In => "Entrada",
        PunchDirection.Out => "Salida",
        _ => "Sin tipo"
    };

    public string SourceLabel => Source switch
    {
        PunchSource.Mock => "Simulador",
        PunchSource.Device => "K30",
        PunchSource.Import => "CSV",
        _ => "Manual"
    };
}

public sealed record Shift(
    long EmployeeId,
    DateTime ClockIn,
    DateTime ClockOut,
    decimal Hours,
    long ClockInPunchId,
    long ClockOutPunchId);

public sealed record PunchIssue(
    long EmployeeId,
    long PunchId,
    DateTime PunchedAt,
    string Message,
    string EmployeeName = "");

public sealed class AppSettings
{
    public bool UseMockDevice { get; set; } = true;
    public string DeviceIp { get; set; } = "192.168.1.201";
    public int DevicePort { get; set; } = 4370;
    public int DeviceNumber { get; set; } = 1;
}

public sealed class WeeklyReportLine
{
    public long EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public decimal Hours { get; set; }
    public decimal Pay { get; set; }
}

public sealed class WeeklyReport
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public IReadOnlyList<WeeklyReportLine> Lines { get; init; } = [];
    public IReadOnlyList<PunchIssue> Issues { get; init; } = [];
    public IReadOnlyList<Shift> Shifts { get; init; } = [];
    public decimal TotalHours => Lines.Sum(l => l.Hours);
    public decimal TotalPay => Lines.Sum(l => l.Pay);
}
