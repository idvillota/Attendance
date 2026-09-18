using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public sealed class AttendanceSyncService : IAttendanceSyncService
{
    private readonly IAttendanceStore _store;

    public AttendanceSyncService(IAttendanceStore store)
    {
        _store = store;
    }

    public IAttendanceDevice CreateDevice(AppSettings settings)
    {
        if (settings.UseMockDevice)
        {
            return new MockAttendanceDevice(() => _store.GetEmployees(activeOnly: true));
        }

        return new ZkTecoNetworkDevice(settings.DeviceIp, settings.DevicePort);
    }

    public async Task<(IAttendanceDevice Device, int Imported)> DownloadConfiguredAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var device = CreateDevice(settings);
        var logs = await device.DownloadLogsAsync(cancellationToken).ConfigureAwait(false);
        var source = settings.UseMockDevice ? PunchSource.Mock : PunchSource.Device;
        return (device, Import(logs, source));
    }

    public async Task<int> DownloadAsync(IAttendanceDevice device, CancellationToken cancellationToken = default)
    {
        var logs = await device.DownloadLogsAsync(cancellationToken).ConfigureAwait(false);
        return Import(logs, PunchSource.Device);
    }

    public int Import(IEnumerable<DevicePunch> logs, PunchSource source)
    {
        var employees = _store.GetEmployees()
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
            inserted += _store.TryAddPunch(employee.Id, log.PunchedAt, log.Direction, source, key);
        }

        return inserted;
    }

    public int ImportCsv(string path) => Import(PunchCsvParser.ParseFile(path), PunchSource.Import);
}
