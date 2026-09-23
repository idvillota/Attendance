using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeeDeleteViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IDeviceUserService _deviceUsers;
    private readonly EmployeeDeletePreview _preview;

    public EmployeeDeleteViewModel(IAttendanceStore store, IDeviceUserService deviceUsers, long employeeId)
    {
        _store = store;
        _deviceUsers = deviceUsers;
        _preview = store.GetEmployeeDeletePreview(employeeId)
            ?? throw new InvalidOperationException("La persona no existe.");
    }

    public string Title => $"Eliminar a {_preview.Name}";
    public string Summary =>
        $"Se quitará {_preview.Name} de Host Asistencia (ID en el K30 {_preview.DeviceUserId}). " +
        "Si solo no debe marcar más, cancele y desactívela en Editar.";
    public string PunchWarning => _preview.PunchCount == 0
        ? string.Empty
        : $"También se borrarán {_preview.PunchCount} marcaciones. Los reportes de semanas pasadas ya no incluirán a esta persona.";
    public string LoginWarning => _preview.LinkedUsernames.Count == 0
        ? string.Empty
        : $"La cuenta {string.Join(", ", _preview.LinkedUsernames)} se desligará y, si es básica, quedará inactiva. No se borra el inicio de sesión.";
    public bool HasPunchWarning => PunchWarning.Length > 0;
    public bool HasLoginWarning => LoginWarning.Length > 0;
    public string DeviceCheckboxLabel =>
        $"También borrar el PIN {_preview.DeviceUserId} en el K30 (huella y clave). Quedará libre.";
    public string ConfirmLabel => DeviceRetryPending ? "Borrar solo en este PC" : "Eliminar";
    public bool Deleted { get; private set; }
    public string ResultStatus { get; private set; } = string.Empty;

    [ObservableProperty]
    private bool _removeFromDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmLabel))]
    private bool _deviceRetryPending;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private string _deviceStatus =
        "Si no marca el reloj, el PIN puede seguir marcando y aparecer como desconocido al cargar logs.";

    public async Task<bool> TryDeleteAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        Error = string.Empty;
        try
        {
            var deviceNote = string.Empty;
            if (RemoveFromDevice && !DeviceRetryPending)
            {
                try
                {
                    var device = await _deviceUsers.DeleteAsync(_store.GetSettings(), _preview.DeviceUserId);
                    if (!device.Succeeded)
                    {
                        DeviceRetryPending = true;
                        DeviceStatus = device.Message;
                        Error = $"{device.Message} Puede borrar solo en este PC; el PIN seguirá en el reloj.";
                        return false;
                    }

                    deviceNote = device.Message;
                }
                catch (Exception ex)
                {
                    DeviceRetryPending = true;
                    DeviceStatus = ex.InnerException?.Message ?? ex.Message;
                    Error = "El reloj no respondió. Puede borrar solo en este PC; el PIN seguirá en el K30.";
                    return false;
                }
            }
            else if (DeviceRetryPending || !RemoveFromDevice)
            {
                deviceNote = $"El PIN {_preview.DeviceUserId} sigue en el K30.";
            }

            var result = _store.DeleteEmployee(_preview.EmployeeId);
            Deleted = true;
            ResultStatus = ComposeStatus(result, deviceNote);
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string ComposeStatus(EmployeeDeleteResult result, string deviceNote)
    {
        var punches = result.RemovedPunches == 0
            ? $"{result.Name} se eliminó de este PC."
            : $"{result.Name} se eliminó de este PC ({result.RemovedPunches} marcaciones).";
        var logins = result.UnlinkedUsernames.Count == 0
            ? string.Empty
            : $" La cuenta {string.Join(", ", result.UnlinkedUsernames)} ya no está ligada.";
        var device = string.IsNullOrWhiteSpace(deviceNote) ? string.Empty : $" {deviceNote}";
        return punches + logins + device;
    }
}
