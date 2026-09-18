using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Desktop.ViewModels;

public partial class RolesViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;

    public RolesViewModel(IAttendanceStore store)
    {
        _store = store;
        Reload();
    }

    public ObservableCollection<Role> Roles { get; } = [];

    public RoleEditorViewModel CreateEditor(Role? role) => new(_store, role);

    public void Reload()
    {
        Roles.Clear();
        foreach (var role in _store.GetRoles())
        {
            Roles.Add(role);
        }
    }
}
