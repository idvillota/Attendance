using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public sealed class AttendanceSyncService : IAttendanceSyncService
{
    private readonly IAttendanceStore _store;
    private readonly IDeviceLogDraft _draft;

    public AttendanceSyncService(IAttendanceStore store, IDeviceLogDraft draft)
    {
        _store = store;
        _draft = draft;
    }

    public IAttendanceDevice CreateDevice(AppSettings settings) =>
        new ZkTecoNetworkDevice(
            string.IsNullOrWhiteSpace(settings.DeviceIp) ? K30Defaults.Ip : settings.DeviceIp,
            settings.DevicePort == 0 ? K30Defaults.Port : settings.DevicePort);

    public async Task<(IAttendanceDevice Device, IReadOnlyList<DevicePunch> Logs)> FetchLogsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var device = CreateDevice(settings);
        var logs = await device.DownloadLogsAsync(cancellationToken).ConfigureAwait(false);
        return (device, logs);
    }

    public void StageLogs(IEnumerable<DevicePunch> logs, string deviceName, PunchSource source)
    {
        _draft.Replace(logs, _store.GetEmployees(activeOnly: true), deviceName, source);
    }

    public async Task<(IAttendanceDevice Device, int Staged)> PullToDraftAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var (device, logs) = await FetchLogsAsync(settings, cancellationToken).ConfigureAwait(false);
        StageLogs(logs, device.Name, PunchSource.Device);
        return (device, logs.Count);
    }

    public int CommitDraft(DateOnly? from = null, DateOnly? to = null)
    {
        var inserted = _draft.Save(_store, from, to);
        RememberDownload();
        return inserted;
    }

    public async Task<(IAttendanceDevice Device, int Imported)> DownloadConfiguredAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var device = CreateDevice(settings);
        var logs = await device.DownloadLogsAsync(cancellationToken).ConfigureAwait(false);
        var imported = Import(logs, PunchSource.Device);
        RememberDownload();
        return (device, imported);
    }

    public async Task<int> DownloadAsync(IAttendanceDevice device, CancellationToken cancellationToken = default)
    {
        var logs = await device.DownloadLogsAsync(cancellationToken).ConfigureAwait(false);
        var imported = Import(logs, PunchSource.Device);
        RememberDownload();
        return imported;
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

    public int ImportCsv(string path)
    {
        var imported = Import(PunchCsvParser.ParseFile(path), PunchSource.Import);
        RememberDownload();
        return imported;
    }

    private void RememberDownload()
    {
        var settings = _store.GetSettings();
        settings.LastDownloadAt = DateTime.Now;
        _store.SaveSettings(settings);
    }
}
