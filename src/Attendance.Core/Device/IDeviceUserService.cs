using Attendance.Core.Domain;

namespace Attendance.Core.Device;

public interface IDeviceUserService
{
    Task<IReadOnlyList<DeviceUser>> ListAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<DeviceUserWriteResult> UpsertAsync(
        AppSettings settings,
        string pin,
        string name,
        string? password,
        bool enabled,
        CancellationToken cancellationToken = default);
    Task<DeviceEnrollResult> EnrollFingerprintAsync(
        AppSettings settings,
        string pin,
        int fingerIndex = 0,
        CancellationToken cancellationToken = default);
    Task<DeviceUserDeleteResult> DeleteAsync(
        AppSettings settings,
        string pin,
        CancellationToken cancellationToken = default);
}

public sealed class DeviceUserService : IDeviceUserService
{
    public Task<IReadOnlyList<DeviceUser>> ListAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        Live(settings).DownloadUsersAsync(cancellationToken);

    public Task<DeviceUserWriteResult> UpsertAsync(
        AppSettings settings,
        string pin,
        string name,
        string? password,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        Live(settings).UpsertUserAsync(pin, name, password, enabled, cancellationToken);

    public Task<DeviceEnrollResult> EnrollFingerprintAsync(
        AppSettings settings,
        string pin,
        int fingerIndex = 0,
        CancellationToken cancellationToken = default) =>
        Live(settings, TimeSpan.FromSeconds(75)).EnrollFingerprintAsync(pin, fingerIndex, cancellationToken: cancellationToken);

    public Task<DeviceUserDeleteResult> DeleteAsync(
        AppSettings settings,
        string pin,
        CancellationToken cancellationToken = default) =>
        Live(settings).DeleteUserAsync(pin, cancellationToken);

    private static ZkTecoNetworkDevice Live(AppSettings settings, TimeSpan? timeout = null) =>
        new(
            string.IsNullOrWhiteSpace(settings.DeviceIp) ? K30Defaults.Ip : settings.DeviceIp,
            settings.DevicePort == 0 ? K30Defaults.Port : settings.DevicePort,
            timeout);
}
