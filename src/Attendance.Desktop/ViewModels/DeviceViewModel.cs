using Attendance.Core;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class DeviceViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;
    private readonly AttendanceSyncService _sync;

    public DeviceViewModel(AttendanceDb db, AttendanceSyncService sync)
    {
        _db = db;
        _sync = sync;
        Reload();
    }

    [ObservableProperty]
    private bool _useMockDevice = true;

    [ObservableProperty]
    private string _deviceIp = "192.168.1.201";

    [ObservableProperty]
    private string _devicePort = "4370";

    [ObservableProperty]
    private string _status = "En este Mac use el simulador. En Windows, ponga la IP del K30.";

    [ObservableProperty]
    private string _databasePath = AppPaths.DatabaseFile;

    public void Reload()
    {
        var settings = _db.GetSettings();
        UseMockDevice = settings.UseMockDevice;
        DeviceIp = settings.DeviceIp;
        DevicePort = settings.DevicePort.ToString();
        DatabasePath = _db.FilePath;
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(DevicePort, out var port) || port is <= 0 or > 65535)
        {
            Status = "Puerto inválido.";
            return;
        }

        _db.SaveSettings(new AppSettings
        {
            UseMockDevice = UseMockDevice,
            DeviceIp = DeviceIp,
            DevicePort = port
        });
        Status = "Configuración guardada.";
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        try
        {
            Save();
            var device = _sync.CreateDevice(_db.GetSettings());
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
            var device = _sync.CreateDevice(_db.GetSettings());
            var inserted = await device.DownloadLogsAsync();
            var count = _sync.Import(inserted, UseMockDevice ? PunchSource.Mock : PunchSource.Device);
            Status = count == 0
                ? $"Sin marcaciones nuevas desde {device.Name}."
                : $"Se importaron {count} marcación(es) desde {device.Name}.";
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
