using System.Collections.ObjectModel;
using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using Attendance.Core.Payroll;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeesViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IDeviceUserService _deviceUsers;
    public bool CanManagePeople { get; }

    public EmployeesViewModel(IAttendanceStore store, IAuthSession session, IDeviceUserService deviceUsers)
    {
        _store = store;
        _deviceUsers = deviceUsers;
        CanManagePeople = session.IsAdmin;
        Reload();
    }

    public ObservableCollection<EmployeePresenceItem> Employees { get; } = [];

    [ObservableProperty]
    private string _freshness = string.Empty;

    public EmployeeEditorViewModel CreateEditor(Employee? employee) => new(_store, _deviceUsers, employee);

    public EmployeeDeleteViewModel CreateDelete(Employee employee) =>
        new(_store, _deviceUsers, employee.Id);

    public void Reload() => Reload(null);

    public void Reload(string? status)
    {
        var people = _store.GetEmployees();
        var byId = people.ToDictionary(employee => employee.Id);
        Employees.Clear();
        foreach (var presence in Presence.Snapshot(people, _store.GetPunches()))
        {
            byId.TryGetValue(presence.EmployeeId, out var employee);
            Employees.Add(new EmployeePresenceItem(presence, employee) { CanEdit = CanManagePeople });
        }

        Freshness = string.IsNullOrWhiteSpace(status)
            ? PresenceStatus.Freshness(_store.GetSettings().LastDownloadAt)
            : status;
    }
}
