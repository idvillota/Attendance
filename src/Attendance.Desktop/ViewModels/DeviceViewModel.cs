using Attendance.Core;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Desktop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class DeviceViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IAttendanceSyncService _sync;
    private readonly IAppNavigator _navigator;

    public DeviceViewModel(IAttendanceStore store, IAttendanceSyncService sync, IAppNavigator navigator)
    {
        _store = store;
        _sync = sync;
        _navigator = navigator;
        Reload();
    }

    [ObservableProperty]
    private string _deviceIp = K30Defaults.Ip;

    [ObservableProperty]
    private string _devicePort = "4370";

    [ObservableProperty]
    private string _status = $"K30 en {K30Defaults.Endpoint}. Descargar ahora abre Logs K30 para revisar; no guarda en nómina hasta que confirme.";

    [ObservableProperty]
    private string _databasePath = AppPaths.DatabaseFile;

    public void Reload()
    {
        var settings = _store.GetSettings();
        DeviceIp = settings.DeviceIp;
        DevicePort = settings.DevicePort.ToString();
        DatabasePath = _store.FilePath;
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(DevicePort, out var port) || port is <= 0 or > 65535)
        {
            Status = "Puerto inválido.";
            return;
        }

        var settings = _store.GetSettings();
        settings.DeviceIp = DeviceIp;
        settings.DevicePort = port;
        _store.SaveSettings(settings);
        Status = "Configuración guardada.";
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        try
        {
            Save();
            var device = _sync.CreateDevice(_store.GetSettings());
            await device.TestConnectionAsync();
            Status = $"Conexión OK: {device.Name}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        try
        {
            Save();
            var (device, count) = await _sync.PullToDraftAsync(_store.GetSettings());
            Status = count == 0
                ? $"Sin marcaciones en {device.Name}."
                : $"{count} marcación(es) de {device.Name} listas para revisar en Logs K30.";
            _navigator.GoToDeviceLogs();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void ImportCsv(string path)
    {
        try
        {
            var count = _sync.ImportCsv(path);
            Status = count == 0
                ? "El CSV no trajo marcaciones nuevas (o el ID del K30 no coincide con un empleado)."
                : $"Se importaron {count} marcación(es) desde el CSV.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}
