using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public sealed class AttendanceSyncService
{
    private readonly AttendanceDb _db;

    public AttendanceSyncService(AttendanceDb db)
    {
        _db = db;
    }

    public IAttendanceDevice CreateDevice(AppSettings settings)
    {
        if (settings.UseMockDevice)
        {
            return new MockAttendanceDevice(() => _db.GetEmployees(activeOnly: true));
        }

        return new ZkTecoNetworkDevice(settings.DeviceIp, settings.DevicePort);
    }

    public async Task<int> DownloadAsync(IAttendanceDevice device, CancellationToken cancellationToken = default)
    {
        var logs = await device.DownloadLogsAsync(cancellationToken).ConfigureAwait(false);
        return Import(logs, PunchSource.Device);
    }

    public int Import(IEnumerable<DevicePunch> logs, PunchSource source)
    {
        var employees = _db.GetEmployees()
            .Where(e => e.IsActive)
            .ToDictionary(e => e.DeviceUserId.Trim(), e => e, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        foreach (var log in logs)
        {
            if (!employees.TryGetValue(log.DeviceUserId.Trim(), out var employee))
            {
                continue;
            }

            var key = log.DeviceKey ?? $"{source}:{employee.DeviceUserId}:{log.PunchedAt:o}";
            inserted += _db.TryAddPunch(employee.Id, log.PunchedAt, log.Direction, source, key);
        }

        return inserted;
    }

    public int ImportCsv(string path)
    {
        var logs = new List<DevicePunch>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("DeviceUserId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',', ';');
            if (parts.Length < 2)
            {
                continue;
            }

            var userId = parts[0].Trim().Trim('"');
            if (!DateTime.TryParse(parts[1].Trim().Trim('"'), out var at))
            {
                continue;
            }

            var direction = PunchDirection.Unknown;
            if (parts.Length >= 3)
            {
                var token = parts[2].Trim().Trim('"');
                direction = token.Equals("in", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("entrada", StringComparison.OrdinalIgnoreCase)
                    ? PunchDirection.In
                    : token.Equals("out", StringComparison.OrdinalIgnoreCase)
                      || token.Equals("salida", StringComparison.OrdinalIgnoreCase)
                        ? PunchDirection.Out
                        : PunchDirection.Unknown;
            }

            logs.Add(new DevicePunch
            {
                DeviceUserId = userId,
                PunchedAt = at,
                Direction = direction,
                DeviceKey = $"csv:{userId}:{at:o}"
            });
        }

        return Import(logs, PunchSource.Import);
    }
}
