using Attendance.Core.Auth;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class UserEditorViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly long? _id;

    public UserEditorViewModel(IAuthService auth, IReadOnlyList<Employee> employees, AppUser? user)
    {
        _auth = auth;
        Employees = employees;
        RoleChoices =
        [
            new AppRoleChoice(AppRole.Admin, "Administrador"),
            new AppRoleChoice(AppRole.Basic, "Básico")
        ];

        if (user is null)
        {
            Title = "Nueva cuenta";
            SelectedRole = RoleChoices[0];
            NeedsEmployee = false;
            return;
        }

        _id = user.Id;
        Title = "Editar cuenta";
        FormUsername = user.Username;
        IsActive = user.IsActive;
        SelectedRole = RoleChoices.First(choice => choice.Value == user.Role);
        SelectedEmployee = Employees.FirstOrDefault(employee => employee.Id == user.EmployeeId);
        NeedsEmployee = SelectedRole.Value == AppRole.Basic;
    }

    public string Title { get; }
    public IReadOnlyList<Employee> Employees { get; }
    public IReadOnlyList<AppRoleChoice> RoleChoices { get; }

    [ObservableProperty] private string _formUsername = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private AppRoleChoice? _selectedRole;
    [ObservableProperty] private Employee? _selectedEmployee;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private bool _needsEmployee;

    partial void OnSelectedRoleChanged(AppRoleChoice? value) =>
        NeedsEmployee = value?.Value == AppRole.Basic;

    public bool TrySave()
    {
        try
        {
            if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                Error = "Las contraseñas no coinciden.";
                return false;
            }

            var role = SelectedRole?.Value ?? AppRole.Admin;
            var employeeId = role == AppRole.Basic ? SelectedEmployee?.Id : null;
            if (_id is null)
            {
                _auth.CreateUser(FormUsername, Password, role, employeeId);
            }
            else
            {
                _auth.UpdateUser(_id.Value, FormUsername, string.IsNullOrWhiteSpace(Password) ? null : Password, role, employeeId, IsActive);
            }

            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return false;
        }
    }
}

public sealed record AppRoleChoice(AppRole Value, string Label);
