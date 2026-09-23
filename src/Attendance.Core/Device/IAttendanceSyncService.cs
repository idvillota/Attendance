using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public interface IAttendanceSyncService
{
    IAttendanceDevice CreateDevice(AppSettings settings);
    Task<(IAttendanceDevice Device, IReadOnlyList<DevicePunch> Logs)> FetchLogsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
    void StageLogs(IEnumerable<DevicePunch> logs, string deviceName, PunchSource source);
    Task<(IAttendanceDevice Device, int Staged)> PullToDraftAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
    int CommitDraft(DateOnly? from = null, DateOnly? to = null);
    Task<(IAttendanceDevice Device, int Imported)> DownloadConfiguredAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
    Task<int> DownloadAsync(IAttendanceDevice device, CancellationToken cancellationToken = default);
    int Import(IEnumerable<DevicePunch> logs, PunchSource source);
    int ImportCsv(string path);
}
