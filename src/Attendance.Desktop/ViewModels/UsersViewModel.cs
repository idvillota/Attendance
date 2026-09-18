using System.Collections.ObjectModel;
using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Desktop.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly IAttendanceStore _store;
    private readonly IAuthSession _session;

    public UsersViewModel(IAuthService auth, IAttendanceStore store, IAuthSession session)
    {
        _auth = auth;
        _store = store;
        _session = session;
        Reload();
    }

    public ObservableCollection<AppUser> Users { get; } = [];

    public UserEditorViewModel CreateEditor(AppUser? user) =>
        new(_auth, _store.GetEmployees(), user);

    public void Reload()
    {
        Users.Clear();
        if (!_session.IsAdmin)
        {
            return;
        }

        foreach (var user in _auth.GetUsers())
        {
            Users.Add(user);
        }
    }
}
