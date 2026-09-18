using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IReadOnlyList<AppSection> _sections;

    public MainViewModel(
        RolesViewModel roles,
        EmployeesViewModel employees,
        PunchesViewModel punches,
        ReportViewModel report,
        DeviceViewModel device,
        ScheduleViewModel schedule)
    {
        Roles = roles;
        Employees = employees;
        Punches = punches;
        Report = report;
        Device = device;
        Schedule = schedule;
        _sections =
        [
            new("Esta semana", report, report.Reload),
            new("Personas", employees, employees.Reload),
            new("Roles", roles, roles.Reload),
            new("Horario y recargos", schedule, schedule.Reload),
            new("Corregir marcaciones", punches, punches.Reload),
            new("Reloj K30", device, device.Reload)
        ];
        NavItems = _sections.Select(s => s.Title).ToArray();
        CurrentPage = report;
        CurrentSection = NavItems[0];
    }

    public string[] NavItems { get; }

    public RolesViewModel Roles { get; }
    public EmployeesViewModel Employees { get; }
    public PunchesViewModel Punches { get; }
    public ReportViewModel Report { get; }
    public DeviceViewModel Device { get; }
    public ScheduleViewModel Schedule { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    [ObservableProperty]
    private string _currentSection = string.Empty;

    [ObservableProperty]
    private int _navIndex;

    partial void OnNavIndexChanged(int value)
    {
        if ((uint)value >= (uint)_sections.Count)
        {
            return;
        }

        var section = _sections[value];
        section.Reload();
        CurrentPage = section.Page;
        CurrentSection = section.Title;
    }

    private sealed record AppSection(string Title, ViewModelBase Page, Action Reload);
}
