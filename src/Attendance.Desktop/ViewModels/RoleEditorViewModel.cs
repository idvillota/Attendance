using Attendance.Core.Data;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class RoleEditorViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;
    private readonly long? _id;

    public RoleEditorViewModel(AttendanceDb db, Role? role)
    {
        _db = db;
        if (role is null)
        {
            Title = "Nuevo rol";
            HourlyRateText = "8000";
            return;
        }

        _id = role.Id;
        Title = "Editar rol";
        FormName = role.Name;
        HourlyRateText = role.HourlyRate.ToString("0");
        IsActive = role.IsActive;
    }

    public string Title { get; }

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _hourlyRateText = "8000";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string _error = string.Empty;

    public bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(FormName) || !decimal.TryParse(HourlyRateText, out var rate) || rate < 0)
        {
            Error = "Nombre y valor por hora son obligatorios.";
            return false;
        }

        if (_id is null)
        {
            _db.AddRole(FormName, rate);
        }
        else
        {
            _db.UpdateRole(new Role
            {
                Id = _id.Value,
                Name = FormName,
                HourlyRate = rate,
                IsActive = IsActive
            });
        }

        return true;
    }
}
