using Attendance.Core.Data;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeeEditorViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly long? _id;

    public EmployeeEditorViewModel(IAttendanceStore store, Employee? employee)
    {
        _store = store;
        Roles = [.. store.GetRoles(activeOnly: true)];
        if (employee is null)
        {
            Title = "Nueva persona";
            SelectedRole = Roles.FirstOrDefault();
            return;
        }

        _id = employee.Id;
        Title = "Editar persona";
        FormName = employee.Name;
        FormDeviceUserId = employee.DeviceUserId;
        IsActive = employee.IsActive;
        SelectedRole = Roles.FirstOrDefault(r => r.Id == employee.RoleId) ?? Roles.FirstOrDefault();
    }

    public string Title { get; }
    public IReadOnlyList<Role> Roles { get; }

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formDeviceUserId = string.Empty;

    [ObservableProperty]
    private Role? _selectedRole;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _error = string.Empty;

    public bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(FormName) || SelectedRole is null || string.IsNullOrWhiteSpace(FormDeviceUserId))
        {
            Error = "Nombre, rol e ID del K30 son obligatorios.";
            return false;
        }

        try
        {
            if (_id is null)
            {
                _store.AddEmployee(FormName, SelectedRole.Id, FormDeviceUserId);
            }
            else
            {
                _store.UpdateEmployee(new Employee
                {
                    Id = _id.Value,
                    Name = FormName,
                    RoleId = SelectedRole.Id,
                    DeviceUserId = FormDeviceUserId,
                    IsActive = IsActive
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            Error = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }
}
