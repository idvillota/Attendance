using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeeEditorViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IDeviceUserService _deviceUsers;
    private readonly long? _id;
    private string _suggestedPin = string.Empty;
    private bool _pinTouched;

    public EmployeeEditorViewModel(IAttendanceStore store, IDeviceUserService deviceUsers, Employee? employee)
    {
        _store = store;
        _deviceUsers = deviceUsers;
        Roles = [.. store.GetRoles(activeOnly: true)];
        if (employee is null)
        {
            Title = "Nueva persona";
            SelectedRole = Roles.FirstOrDefault();
            ClockPassword = K30UserRecord.NewPassword();
            SuggestPinFrom(LocalPins());
            _ = SuggestPinFromDeviceAsync();
            return;
        }

        _id = employee.Id;
        Title = "Editar persona";
        FormName = employee.Name;
        FormDeviceUserId = employee.DeviceUserId;
        IsActive = employee.IsActive;
        SelectedRole = Roles.FirstOrDefault(r => r.Id == employee.RoleId) ?? Roles.FirstOrDefault();
        _ = LoadDeviceAsync();
    }

    public string Title { get; }
    public bool CanDelete => _id is not null;
    public string DeleteStatus { get; set; } = string.Empty;
    public IReadOnlyList<Role> Roles { get; }

    public EmployeeDeleteViewModel CreateDelete() =>
        new(_store, _deviceUsers, _id ?? throw new InvalidOperationException("Solo se puede eliminar una persona guardada."));

    [ObservableProperty]
    private bool _saved;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formDeviceUserId = string.Empty;

    [ObservableProperty]
    private Role? _selectedRole;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _syncToDevice = true;

    [ObservableProperty]
    private string _clockPassword = string.Empty;

    [ObservableProperty]
    private bool _canEnroll;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _deviceStatus = "Al guardar se escribe en este PC y, si el reloj responde, también en el K30.";

    [ObservableProperty]
    private string _error = string.Empty;

    [RelayCommand]
    private void GeneratePassword() => ClockPassword = K30UserRecord.NewPassword();

    [RelayCommand]
    private void SuggestPin()
    {
        _pinTouched = false;
        SuggestPinFrom(LocalPins());
        _ = SuggestPinFromDeviceAsync();
    }

    partial void OnFormDeviceUserIdChanged(string value)
    {
        if (_id is not null)
        {
            return;
        }

        _pinTouched = !string.Equals(value, _suggestedPin, StringComparison.Ordinal);
    }

    public async Task<bool> TrySaveAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormName) || SelectedRole is null || string.IsNullOrWhiteSpace(FormDeviceUserId))
        {
            Error = "Nombre, rol e ID del K30 son obligatorios.";
            return false;
        }

        var pin = FormDeviceUserId.Trim();
        if (SyncToDevice && !K30UserRecord.IsValidPin(pin))
        {
            Error = "El ID del K30 debe ser numérico (1 a 9 dígitos).";
            return false;
        }

        IsBusy = true;
        Error = string.Empty;
        try
        {
            if (_id is null)
            {
                _store.AddEmployee(FormName, SelectedRole.Id, pin);
            }
            else
            {
                _store.UpdateEmployee(new Employee
                {
                    Id = _id.Value,
                    Name = FormName,
                    RoleId = SelectedRole.Id,
                    DeviceUserId = pin,
                    IsActive = IsActive
                });
            }

            Saved = true;
            if (!SyncToDevice)
            {
                DeviceStatus = "Guardado en este PC. No se envió al reloj.";
                return true;
            }

            var password = string.IsNullOrWhiteSpace(ClockPassword) ? K30UserRecord.NewPassword() : ClockPassword.Trim();
            ClockPassword = password;
            var written = await _deviceUsers.UpsertAsync(_store.GetSettings(), pin, FormName, password, IsActive);
            CanEnroll = true;
            DeviceStatus = written.Created
                ? $"En el K30 y en este PC. Clave temporal: {written.Password}. Puede marcar ya, o registrar huella."
                : $"Actualizado en el K30. Clave: {written.Password}. Puede registrar huella si aún no tiene.";
            return true;
        }
        catch (Exception ex)
        {
            Error = Saved
                ? $"Guardado en este PC. El reloj no se actualizó: {ex.InnerException?.Message ?? ex.Message}"
                : ex.InnerException?.Message ?? ex.Message;
            return Saved;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EnrollAsync()
    {
        if (IsBusy || !CanEnroll)
        {
            return;
        }

        IsBusy = true;
        Error = string.Empty;
        DeviceStatus = "Ponga el mismo dedo en el K30 tres veces y espere cada confirmación. No cierre esta ventana.";
        try
        {
            var result = await _deviceUsers.EnrollFingerprintAsync(_store.GetSettings(), FormDeviceUserId.Trim());
            DeviceStatus = result.Succeeded
                ? result.Message
                : $"{result.Message} Clave temporal: {ClockPassword}";
            if (!result.Succeeded)
            {
                Error = result.Message;
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            DeviceStatus = $"No se inscribió la huella. Use la clave {ClockPassword}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDeviceAsync()
    {
        try
        {
            var users = await _deviceUsers.ListAsync(_store.GetSettings());
            var match = users.FirstOrDefault(user =>
                user.Pin.Equals(FormDeviceUserId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                DeviceStatus = "Esta persona aún no está en el K30. Al guardar se crea con una clave temporal.";
                if (string.IsNullOrWhiteSpace(ClockPassword))
                {
                    ClockPassword = K30UserRecord.NewPassword();
                }

                return;
            }

            CanEnroll = true;
            DeviceStatus = match.HasPassword
                ? $"Ya está en el K30 como {match.Name}. Deje la clave vacía para no cambiarla, o genere otra."
                : $"Ya está en el K30 como {match.Name}, sin clave. Genere una o registre huella.";
        }
        catch (Exception ex)
        {
            DeviceStatus = $"No se pudo leer el K30 ({ex.Message}). Igual puede guardar en este PC.";
        }
    }

    private async Task SuggestPinFromDeviceAsync()
    {
        try
        {
            var devicePins = (await _deviceUsers.ListAsync(_store.GetSettings()))
                .Select(user => user.Pin);
            SuggestPinFrom(LocalPins().Concat(devicePins));
        }
        catch (Exception)
        {
            DeviceStatus = $"ID libre sugerido: {FormDeviceUserId}. No se pudo leer el K30; confirme que ese PIN no exista en el reloj.";
        }
    }

    private void SuggestPinFrom(IEnumerable<string> usedPins)
    {
        if (_id is not null || _pinTouched)
        {
            return;
        }

        var pin = K30UserRecord.NextFreePin(usedPins);
        _suggestedPin = pin;
        FormDeviceUserId = pin;
        DeviceStatus = $"ID libre sugerido: {pin} (el más bajo que no está en este PC ni en el K30). Puede cambiarlo.";
    }

    private IEnumerable<string> LocalPins() =>
        _store.GetEmployees().Select(employee => employee.DeviceUserId);
}
