using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public sealed class DevicePunch
{
    public required string DeviceUserId { get; init; }
    public required DateTime PunchedAt { get; init; }
    public PunchDirection Direction { get; init; } = PunchDirection.Unknown;
    public int VerifyMode { get; init; }
    public int Status { get; init; }
    public string? DeviceKey { get; init; }

    public string VerifyModeLabel => VerifyMode switch
    {
        0 => "Clave",
        1 => "Huella",
        2 => "Tarjeta",
        _ => $"Otro ({VerifyMode})"
    };

    public string StatusLabel => Status switch
    {
        0 => "Entrada",
        1 => "Salida",
        2 => "Salida descanso",
        3 => "Entrada descanso",
        4 => "OT entrada",
        5 => "OT salida",
        _ => Direction switch
        {
            PunchDirection.In => "Entrada",
            PunchDirection.Out => "Salida",
            _ => $"Otro ({Status})"
        }
    };
}

public interface IAttendanceDevice
{
    string Name { get; }
    Task TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DevicePunch>> DownloadLogsAsync(CancellationToken cancellationToken = default);
}
