namespace Attendance.Desktop;

public interface IAppNavigator
{
    void GoToDeviceLogs();
}

public sealed class AppNavigator : IAppNavigator
{
    public Action? GoToDeviceLogsHandler { get; set; }

    public void GoToDeviceLogs() => GoToDeviceLogsHandler?.Invoke();
}
