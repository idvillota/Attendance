using Attendance.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IReadOnlyList<AppSection> _sections;

    public MainViewModel(
        IAuthSession session,
        RolesViewModel roles,
        EmployeesViewModel employees,
        PunchesViewModel punches,
        ReportViewModel report,
        DeviceViewModel device,
        ScheduleViewModel schedule,
        UsersViewModel users)
    {
        Session = session;
        Roles = roles;
        Employees = employees;
        Punches = punches;
        Report = report;
        Device = device;
        Schedule = schedule;
        Users = users;

        var signedIn = session.User;
        UserLabel = signedIn?.Username ?? "";
        RoleLabel = signedIn?.RoleLabel ?? "";

        _sections = session.IsAdmin
            ?
            [
                new("Esta semana", report, report.Reload),
                new("Personas", employees, employees.Reload),
                new("Roles", roles, roles.Reload),
                new("Horario y recargos", schedule, schedule.Reload),
                new("Corregir marcaciones", punches, punches.Reload),
                new("Reloj K30", device, device.Reload),
                new("Usuarios", users, users.Reload)
            ]
            :
            [
                new("Mi semana", report, report.Reload),
                new("Mis marcaciones", punches, punches.Reload)
            ];

        NavItems = _sections.Select(section => section.Title).ToArray();
        CurrentPage = _sections[0].Page;
        CurrentSection = _sections[0].Title;
    }

    public IAuthSession Session { get; }
    public string UserLabel { get; }
    public string RoleLabel { get; }
    public string[] NavItems { get; }

    public RolesViewModel Roles { get; }
    public EmployeesViewModel Employees { get; }
    public PunchesViewModel Punches { get; }
    public ReportViewModel Report { get; }
    public DeviceViewModel Device { get; }
    public ScheduleViewModel Schedule { get; }
    public UsersViewModel Users { get; }

    public event Action? LogoutRequested;

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

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke();

    private sealed record AppSection(string Title, ViewModelBase Page, Action Reload);
}
