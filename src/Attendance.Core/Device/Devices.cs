using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public sealed class DevicePunch
{
    public required string DeviceUserId { get; init; }
    public required DateTime PunchedAt { get; init; }
    public PunchDirection Direction { get; init; } = PunchDirection.Unknown;
    public string? DeviceKey { get; init; }
}

public interface IAttendanceDevice
{
    string Name { get; }
    Task TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DevicePunch>> DownloadLogsAsync(CancellationToken cancellationToken = default);
}

public sealed class MockAttendanceDevice : IAttendanceDevice
{
    private readonly Func<IReadOnlyList<Employee>> _employees;

    public MockAttendanceDevice(Func<IReadOnlyList<Employee>> employees)
    {
        _employees = employees;
    }

    public string Name => "Simulador (Mac / pruebas)";

    public Task TestConnectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<DevicePunch>> DownloadLogsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var list = new List<DevicePunch>();

        foreach (var employee in _employees().Where(e => e.IsActive))
        {
            var clockIn = today.ToDateTime(new TimeOnly(8, 5)).AddMinutes(employee.Id);
            var clockOut = now.Hour >= 16
                ? today.ToDateTime(new TimeOnly(16, 2)).AddMinutes(employee.Id)
                : now.AddMinutes(-5);

            list.Add(Create(employee, clockIn, PunchDirection.In));
            if (now.Hour >= 12)
            {
                list.Add(Create(employee, clockOut, PunchDirection.Out));
            }
        }

        return Task.FromResult<IReadOnlyList<DevicePunch>>(list);
    }

    private static DevicePunch Create(Employee employee, DateTime at, PunchDirection direction) => new()
    {
        DeviceUserId = employee.DeviceUserId,
        PunchedAt = at,
        Direction = direction,
        DeviceKey = $"mock:{employee.DeviceUserId}:{at:yyyyMMddHHmmss}:{direction}"
    };
}
