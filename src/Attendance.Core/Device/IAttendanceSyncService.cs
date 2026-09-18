using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public interface IAttendanceSyncService
{
    IAttendanceDevice CreateDevice(AppSettings settings);
    Task<(IAttendanceDevice Device, int Imported)> DownloadConfiguredAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
    Task<int> DownloadAsync(IAttendanceDevice device, CancellationToken cancellationToken = default);
    int Import(IEnumerable<DevicePunch> logs, PunchSource source);
    int ImportCsv(string path);
}
