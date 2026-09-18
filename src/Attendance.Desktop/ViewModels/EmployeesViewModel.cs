using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Domain;
using Attendance.Core.Payroll;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeesViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;

    public EmployeesViewModel(IAttendanceStore store)
    {
        _store = store;
        Reload();
    }

    public ObservableCollection<EmployeePresenceItem> Employees { get; } = [];

    [ObservableProperty]
    private string _freshness = string.Empty;

    public EmployeeEditorViewModel CreateEditor(Employee? employee) => new(_store, employee);

    public void Reload()
    {
        var people = _store.GetEmployees();
        var byId = people.ToDictionary(employee => employee.Id);
        Employees.Clear();
        foreach (var status in Presence.Snapshot(people, _store.GetPunches()))
        {
            byId.TryGetValue(status.EmployeeId, out var employee);
            Employees.Add(new EmployeePresenceItem(status, employee));
        }

        Freshness = PresenceStatus.Freshness(_store.GetSettings().LastDownloadAt);
    }
}
