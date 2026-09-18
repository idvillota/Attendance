using Attendance.Core.Domain;

namespace Attendance.Core.Payroll;

public enum PresenceKind
{
    Disabled,
    NotCheckedIn,
    CheckedIn
}

public sealed record PresenceStatus(
    long EmployeeId,
    string Name,
    string RoleName,
    PresenceKind Kind,
    DateTime? ClockedInAt)
{
    public string Label => Kind switch
    {
        PresenceKind.Disabled => "Inactivo",
        PresenceKind.CheckedIn => ClockedInAt is { } at
            ? $"En turno desde {at:HH:mm}"
            : "En turno",
        _ => "Fuera"
    };

    public static string Freshness(DateTime? lastDownloadAt)
    {
        var baseText = "Semáforo según las marcaciones guardadas en este PC (no es en vivo).";
        return lastDownloadAt is { } at
            ? $"{baseText} Última descarga del reloj: {at:dd/MM/yyyy HH:mm}."
            : $"{baseText} Todavía no hay una descarga del K30.";
    }
}

public static class Presence
{
    public static IReadOnlyList<PresenceStatus> Snapshot(
        IEnumerable<Employee> employees,
        IEnumerable<Punch> punches)
    {
        var openByEmployee = ShiftPairing.Pair(punches).OpenIns
            .ToDictionary(punch => punch.EmployeeId);

        return employees
            .OrderBy(employee => employee.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(employee =>
            {
                if (!employee.IsActive)
                {
                    return new PresenceStatus(
                        employee.Id,
                        employee.Name,
                        employee.RoleName,
                        PresenceKind.Disabled,
                        null);
                }

                if (openByEmployee.TryGetValue(employee.Id, out var open))
                {
                    return new PresenceStatus(
                        employee.Id,
                        employee.Name,
                        employee.RoleName,
                        PresenceKind.CheckedIn,
                        open.PunchedAt);
                }

                return new PresenceStatus(
                    employee.Id,
                    employee.Name,
                    employee.RoleName,
                    PresenceKind.NotCheckedIn,
                    null);
            })
            .ToList();
    }
}
