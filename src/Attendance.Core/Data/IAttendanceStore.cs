using Attendance.Core.Domain;

namespace Attendance.Core.Data;

public interface IAttendanceStore
{
    string FilePath { get; }
    void Initialize();
    IReadOnlyList<Role> GetRoles(bool activeOnly = false);
    Role AddRole(string name, RoleRates? rates = null);
    void UpdateRole(Role role);
    IReadOnlyList<Employee> GetEmployees(bool activeOnly = false);
    Employee AddEmployee(string name, long roleId, string deviceUserId);
    void UpdateEmployee(Employee employee);
    IReadOnlyList<Punch> GetPunches(DateTime? from = null, DateTime? toExclusive = null);
    Punch AddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey = null);
    int TryAddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey);
    void DeletePunch(long id);
    AppSettings GetSettings();
    void SaveSettings(AppSettings settings);
    TenantSchedule GetSchedule();
    void SaveSchedule(TenantSchedule schedule);
}
