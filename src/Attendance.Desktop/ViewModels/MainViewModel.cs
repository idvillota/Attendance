using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendance.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
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
        CurrentPage = report;
        CurrentSection = NavItems[0];
    }

    public string[] NavItems { get; } =
    [
        "Esta semana",
        "Personas",
        "Roles",
        "Horario y recargos",
        "Corregir marcaciones",
        "Reloj K30"
    ];

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
        switch (value)
        {
            case 0:
                Report.Reload();
                CurrentPage = Report;
                CurrentSection = NavItems[0];
                break;
            case 1:
                Employees.Reload();
                CurrentPage = Employees;
                CurrentSection = NavItems[1];
                break;
            case 2:
                Roles.Reload();
                CurrentPage = Roles;
                CurrentSection = NavItems[2];
                break;
            case 3:
                Schedule.Reload();
                CurrentPage = Schedule;
                CurrentSection = NavItems[3];
                break;
            case 4:
                Punches.Reload();
                CurrentPage = Punches;
                CurrentSection = NavItems[4];
                break;
            case 5:
                Device.Reload();
                CurrentPage = Device;
                CurrentSection = NavItems[5];
                break;
        }
    }
}
