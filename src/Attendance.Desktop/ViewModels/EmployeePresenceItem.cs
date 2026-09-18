using Attendance.Core.Domain;
using Attendance.Core.Payroll;
using Avalonia.Media;

namespace Attendance.Desktop.ViewModels;

public sealed class EmployeePresenceItem
{
    public EmployeePresenceItem(PresenceStatus status, Employee? employee = null)
    {
        Status = status;
        Employee = employee;
        Fill = status.Kind switch
        {
            PresenceKind.Disabled => Brush("#98A2B3"),
            PresenceKind.CheckedIn => Brush("#12B76A"),
            _ => Brush("#F5A524")
        };
    }

    public PresenceStatus Status { get; }
    public Employee? Employee { get; }
    public IBrush Fill { get; }
    public string Name => Status.Name;
    public string Subtitle => Employee?.Subtitle ?? Status.RoleName;
    public string PresenceLabel => Status.Label;
    public bool CanEdit { get; init; }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
