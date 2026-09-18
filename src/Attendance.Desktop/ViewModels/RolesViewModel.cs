using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Desktop.ViewModels;

public partial class RolesViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;

    public RolesViewModel(AttendanceDb db)
    {
        _db = db;
        Reload();
    }

    public ObservableCollection<Role> Roles { get; } = [];

    public RoleEditorViewModel CreateEditor(Role? role) => new(_db, role);

    public void Reload()
    {
        Roles.Clear();
        foreach (var role in _db.GetRoles())
        {
            Roles.Add(role);
        }
    }
}
