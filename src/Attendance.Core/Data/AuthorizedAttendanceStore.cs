using Attendance.Core.Auth;
using Attendance.Core.Domain;

namespace Attendance.Core.Data;

public sealed class AuthorizedAttendanceStore : IAttendanceStore
{
    private readonly AttendanceDb _inner;
    private readonly IAuthSession _session;

    public AuthorizedAttendanceStore(AttendanceDb inner, IAuthSession session)
    {
        _inner = inner;
        _session = session;
    }

    public string FilePath => _inner.FilePath;

    public void Initialize() => _inner.Initialize();

    public IReadOnlyList<Role> GetRoles(bool activeOnly = false)
    {
        EnsureSignedIn();
        var roles = _inner.GetRoles(activeOnly);
        if (_session.IsAdmin)
        {
            return roles;
        }

        var mine = CurrentEmployee();
        return mine is null
            ? []
            : roles.Where(role => role.Id == mine.RoleId).ToList();
    }

    public Role AddRole(string name, RoleRates? rates = null)
    {
        EnsureAdmin();
        return _inner.AddRole(name, rates);
    }

    public void UpdateRole(Role role)
    {
        EnsureAdmin();
        _inner.UpdateRole(role);
    }

    public IReadOnlyList<Employee> GetEmployees(bool activeOnly = false)
    {
        EnsureSignedIn();
        if (_session.IsAdmin)
        {
            return _inner.GetEmployees(activeOnly);
        }

        var mine = CurrentEmployee();
        if (mine is null)
        {
            return [];
        }

        return activeOnly && !mine.IsActive ? [] : [mine];
    }

    public Employee AddEmployee(string name, long roleId, string deviceUserId)
    {
        EnsureAdmin();
        return _inner.AddEmployee(name, roleId, deviceUserId);
    }

    public void UpdateEmployee(Employee employee)
    {
        EnsureAdmin();
        _inner.UpdateEmployee(employee);
    }

    public EmployeeDeletePreview? GetEmployeeDeletePreview(long employeeId)
    {
        EnsureAdmin();
        return _inner.GetEmployeeDeletePreview(employeeId);
    }

    public EmployeeDeleteResult DeleteEmployee(long employeeId)
    {
        EnsureAdmin();
        return _inner.DeleteEmployee(employeeId);
    }

    public IReadOnlyList<Punch> GetPunches(DateTime? from = null, DateTime? toExclusive = null)
    {
        EnsureSignedIn();
        var punches = _inner.GetPunches(from, toExclusive);
        if (_session.IsAdmin)
        {
            return punches;
        }

        var id = _session.EmployeeId;
        return id is null ? [] : punches.Where(punch => punch.EmployeeId == id.Value).ToList();
    }

    public Punch AddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey = null)
    {
        EnsureAdmin();
        return _inner.AddPunch(employeeId, punchedAt, direction, source, deviceKey);
    }

    public int TryAddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey)
    {
        EnsureAdmin();
        return _inner.TryAddPunch(employeeId, punchedAt, direction, source, deviceKey);
    }

    public void DeletePunch(long id)
    {
        EnsureAdmin();
        _inner.DeletePunch(id);
    }

    public AppSettings GetSettings()
    {
        EnsureSignedIn();
        var settings = _inner.GetSettings();
        if (_session.IsAdmin)
        {
            return settings;
        }

        return new AppSettings
        {
            LastDownloadAt = settings.LastDownloadAt,
            Schedule = settings.Schedule
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        EnsureAdmin();
        _inner.SaveSettings(settings);
    }

    public TenantSchedule GetSchedule()
    {
        EnsureSignedIn();
        return _inner.GetSchedule();
    }

    public void SaveSchedule(TenantSchedule schedule)
    {
        EnsureAdmin();
        _inner.SaveSchedule(schedule);
    }

    private Employee? CurrentEmployee()
    {
        if (_session.EmployeeId is not { } id)
        {
            return null;
        }

        return _inner.GetEmployees().FirstOrDefault(employee => employee.Id == id);
    }

    private void EnsureSignedIn()
    {
        if (!_session.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Inicie sesión para continuar.");
        }
    }

    private void EnsureAdmin()
    {
        EnsureSignedIn();
        if (!_session.IsAdmin)
        {
            throw new UnauthorizedAccessException("No tiene permiso para esta acción.");
        }
    }
}
