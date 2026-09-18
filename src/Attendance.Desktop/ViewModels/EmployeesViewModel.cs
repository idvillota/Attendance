using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Desktop.ViewModels;

public partial class EmployeesViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;

    public EmployeesViewModel(AttendanceDb db)
    {
        _db = db;
        Reload();
    }

    public ObservableCollection<Employee> Employees { get; } = [];

    public EmployeeEditorViewModel CreateEditor(Employee? employee) => new(_db, employee);

    public void Reload()
    {
        Employees.Clear();
        foreach (var employee in _db.GetEmployees())
        {
            Employees.Add(employee);
        }
    }
}
