using Attendance.Core.Data;
using Attendance.Core.Domain;
using Attendance.Core.Payroll;

namespace Attendance.Core.Device;

public interface IDeviceLogDraft
{
    int Version { get; }
    string DeviceName { get; }
    PunchSource Source { get; }
    bool HasItems { get; }
    IReadOnlyList<DeviceLogDraftItem> Items { get; }
    void Replace(IEnumerable<DevicePunch> punches, IEnumerable<Employee> employees, string deviceName, PunchSource source);
    void Remove(DeviceLogDraftItem item);
    void UpdateTime(DeviceLogDraftItem item, DateTime punchedAt);
    void Assign(DeviceLogDraftItem item, Employee employee);
    void AssignDeviceUser(string deviceUserId, Employee employee);
    IReadOnlyList<DeviceLogDraftItem> ItemsInRange(DateOnly? from, DateOnly? to);
    DeviceLogPreview Preview(DateOnly? from = null, DateOnly? to = null);
    int Save(IAttendanceStore store, DateOnly? from = null, DateOnly? to = null);
}

public sealed class DeviceLogDraftItem
{
    public int RecordNumber { get; set; }
    public string DeviceUserId { get; set; } = string.Empty;
    public string? DeviceKey { get; set; }
    public DateTime PunchedAt { get; set; }
    public string VerifyModeLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public long? EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsMatched => EmployeeId is not null;
    public string MatchLabel => IsMatched ? "Sí" : "No";
}

public sealed record ProposedShiftRow(
    long EmployeeId,
    string EmployeeName,
    DateTime ClockIn,
    DateTime ClockOut,
    decimal Hours);

public sealed record DeviceLogPreview(
    IReadOnlyList<ProposedShiftRow> Shifts,
    IReadOnlyList<PunchIssue> Issues,
    int MatchedCount,
    int UnmatchedCount,
    decimal TotalHours);

public sealed class DeviceLogDraft : IDeviceLogDraft
{
    private readonly List<DeviceLogDraftItem> _items = [];

    public int Version { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public PunchSource Source { get; private set; } = PunchSource.Device;
    public bool HasItems => _items.Count > 0;
    public IReadOnlyList<DeviceLogDraftItem> Items => _items;

    public void Replace(
        IEnumerable<DevicePunch> punches,
        IEnumerable<Employee> employees,
        string deviceName,
        PunchSource source)
    {
        var byDeviceId = employees
            .Where(employee => employee.IsActive && !string.IsNullOrWhiteSpace(employee.DeviceUserId))
            .ToDictionary(employee => employee.DeviceUserId.Trim(), employee => employee, StringComparer.OrdinalIgnoreCase);

        _items.Clear();
        var number = 1;
        foreach (var punch in punches.OrderBy(item => item.PunchedAt).ThenBy(item => item.DeviceUserId))
        {
            byDeviceId.TryGetValue(punch.DeviceUserId.Trim(), out var employee);
            _items.Add(new DeviceLogDraftItem
            {
                RecordNumber = number++,
                DeviceUserId = punch.DeviceUserId,
                DeviceKey = punch.DeviceKey,
                PunchedAt = punch.PunchedAt,
                VerifyModeLabel = punch.VerifyModeLabel,
                StatusLabel = punch.StatusLabel,
                EmployeeId = employee?.Id,
                EmployeeName = string.IsNullOrWhiteSpace(employee?.Name) ? "(sin persona)" : employee.Name
            });
        }

        DeviceName = deviceName;
        Source = source;
        Version++;
    }

    public void Remove(DeviceLogDraftItem item)
    {
        if (_items.Remove(item))
        {
            Renumber();
        }
    }

    public void UpdateTime(DeviceLogDraftItem item, DateTime punchedAt)
    {
        item.PunchedAt = punchedAt;
    }

    public void Assign(DeviceLogDraftItem item, Employee employee)
    {
        item.EmployeeId = employee.Id;
        item.EmployeeName = employee.Name;
    }

    public void AssignDeviceUser(string deviceUserId, Employee employee)
    {
        foreach (var item in _items.Where(row =>
                     row.DeviceUserId.Equals(deviceUserId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            Assign(item, employee);
        }
    }

    public IReadOnlyList<DeviceLogDraftItem> ItemsInRange(DateOnly? from, DateOnly? to) =>
        Scoped(_items, from, to).ToList();

    public DeviceLogPreview Preview(DateOnly? from = null, DateOnly? to = null)
    {
        var scoped = ItemsInRange(from, to);
        var punches = ToCheckpoints(scoped);
        var names = scoped
            .Where(item => item.EmployeeId is not null)
            .GroupBy(item => item.EmployeeId!.Value)
            .ToDictionary(group => group.Key, group => group.First().EmployeeName);

        var paired = ShiftPairing.Pair(punches);
        var shifts = paired.Shifts
            .Select(shift => new ProposedShiftRow(
                shift.EmployeeId,
                names.GetValueOrDefault(shift.EmployeeId, ""),
                shift.ClockIn,
                shift.ClockOut,
                shift.Hours))
            .ToList();

        var issues = paired.Issues
            .Select(issue => issue with { EmployeeName = names.GetValueOrDefault(issue.EmployeeId, "") })
            .ToList();

        return new DeviceLogPreview(
            shifts,
            issues,
            scoped.Count(item => item.IsMatched),
            scoped.Count(item => !item.IsMatched),
            decimal.Round(shifts.Sum(shift => shift.Hours), 2));
    }

    public int Save(IAttendanceStore store, DateOnly? from = null, DateOnly? to = null)
    {
        var inserted = 0;
        foreach (var item in ItemsInRange(from, to).Where(row => row.EmployeeId is not null))
        {
            var key = item.DeviceKey ?? $"{Source}:{item.DeviceUserId}:{item.PunchedAt:o}";
            inserted += store.TryAddPunch(
                item.EmployeeId!.Value,
                item.PunchedAt,
                PunchDirection.Unknown,
                Source,
                key);
        }

        return inserted;
    }

    public static bool IsInRange(DateTime punchedAt, DateOnly from, DateOnly to)
    {
        var day = DateOnly.FromDateTime(punchedAt);
        return day >= from && day <= to;
    }

    private static IEnumerable<DeviceLogDraftItem> Scoped(
        IEnumerable<DeviceLogDraftItem> items,
        DateOnly? from,
        DateOnly? to)
    {
        if (from is null || to is null)
        {
            return items;
        }

        if (to.Value < from.Value)
        {
            return [];
        }

        return items.Where(item => IsInRange(item.PunchedAt, from.Value, to.Value));
    }

    private IReadOnlyList<Punch> ToCheckpoints(IEnumerable<DeviceLogDraftItem> items) =>
        items
            .Where(item => item.EmployeeId is not null)
            .OrderBy(item => item.PunchedAt)
            .Select((item, index) => new Punch
            {
                Id = index + 1,
                EmployeeId = item.EmployeeId!.Value,
                EmployeeName = item.EmployeeName,
                PunchedAt = item.PunchedAt,
                Direction = PunchDirection.Unknown,
                Source = Source,
                DeviceKey = item.DeviceKey
            })
            .ToList();

    private void Renumber()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].RecordNumber = i + 1;
        }
    }
}
