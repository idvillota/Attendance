using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeesViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;

    public EmployeesViewModel(IAttendanceStore store)
    {
        _store = store;
        Reload();
    }

    public ObservableCollection<Employee> Employees { get; } = [];

    public EmployeeEditorViewModel CreateEditor(Employee? employee) => new(_store, employee);

    public void Reload()
    {
        Employees.Clear();
        foreach (var employee in _store.GetEmployees())
        {
            Employees.Add(employee);
        }
    }
}
