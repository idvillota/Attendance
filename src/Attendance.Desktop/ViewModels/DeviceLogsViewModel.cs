using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using Attendance.Core.Payroll;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class DeviceLogsViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IAttendanceSyncService _sync;
    private readonly IDeviceLogDraft _draft;

    public DeviceLogsViewModel(IAttendanceStore store, IAttendanceSyncService sync, IDeviceLogDraft draft)
    {
        _store = store;
        _sync = sync;
        _draft = draft;
        var monday = WeekRange.StartOfWeek(WeekRange.Today());
        _fromDate = ToPicker(monday);
        _toDate = ToPicker(WeekRange.EndOfWeek(monday));
        ReloadEmployees();
        Reload();
    }

    public ObservableCollection<DeviceLogRow> Logs { get; } = [];
    public ObservableCollection<ProposedShiftRow> ProposedShifts { get; } = [];
    public ObservableCollection<PunchIssue> Issues { get; } = [];
    public ObservableCollection<Employee> Employees { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasIssues;

    [ObservableProperty]
    private bool _canSave;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private DeviceLogRow? _selected;

    [ObservableProperty]
    private DateTimeOffset? _fromDate;

    [ObservableProperty]
    private DateTimeOffset? _toDate;

    [ObservableProperty]
    private DateTimeOffset? _editDate;

    [ObservableProperty]
    private TimeSpan? _editTime;

    [ObservableProperty]
    private Employee? _assignEmployee;

    [ObservableProperty]
    private string _previewSummary = string.Empty;

    [ObservableProperty]
    private string _status = $"Leyendo el K30 en {K30Defaults.Ip}.";

    public void Reload()
    {
        ReloadEmployees();
        if (_draft.HasItems)
        {
            RefreshFromDraft();
            return;
        }

        _ = LoadAsync();
    }

    [RelayCommand]
    private void ThisWeek()
    {
        var monday = WeekRange.StartOfWeek(WeekRange.Today());
        FromDate = ToPicker(monday);
        ToDate = ToPicker(WeekRange.EndOfWeek(monday));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "Leyendo el reloj...";
        var version = _draft.Version;
        try
        {
            var settings = _store.GetSettings();
            settings.DeviceIp = string.IsNullOrWhiteSpace(settings.DeviceIp) ? K30Defaults.Ip : settings.DeviceIp;
            settings.DevicePort = settings.DevicePort == 0 ? K30Defaults.Port : settings.DevicePort;
            Status = "Leyendo el K30...";
            var (device, punches) = await _sync.FetchLogsAsync(settings);
            if (_draft.Version != version)
            {
                RefreshFromDraft();
                Status = RangeStatus($"{_draft.Items.Count} leídas. No se importaron a nómina.");
                return;
            }

            _sync.StageLogs(punches, device.Name, PunchSource.Device);
            RefreshFromDraft();
            Status = punches.Count == 0
                ? $"Sin marcaciones en {device.Name}."
                : RangeStatus($"{punches.Count} leídas de {device.Name}.");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApplyEdit()
    {
        if (Selected is null)
        {
            return;
        }

        var date = (EditDate?.LocalDateTime.Date ?? Selected.PunchedAt.Date)
                   + (EditTime ?? Selected.PunchedAt.TimeOfDay);
        _draft.UpdateTime(Selected.Item, date);

        if (AssignEmployee is not null)
        {
            if (!Selected.IsMatched)
            {
                _draft.AssignDeviceUser(Selected.DeviceUserId, AssignEmployee);
                AssignEmployee.DeviceUserId = Selected.DeviceUserId;
                _store.UpdateEmployee(AssignEmployee);
                ReloadEmployees();
            }
            else
            {
                _draft.Assign(Selected.Item, AssignEmployee);
            }
        }

        var stayed = InCurrentRange(Selected.Item);
        RefreshFromDraft(keepSelection: Selected.Item);
        Status = stayed
            ? "Cambio aplicado en la vista previa. Todavía no se guardó en nómina."
            : "Cambio aplicado. Esa hora quedó fuera del rango y no se muestra.";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (Selected is null)
        {
            return;
        }

        _draft.Remove(Selected.Item);
        RefreshFromDraft();
        Status = "Marcación quitada de la vista previa. Todavía no se guardó en nómina.";
    }

    [RelayCommand]
    private void Save()
    {
        if (!CanSave)
        {
            return;
        }

        var (from, to) = CurrentRange();
        var preview = _draft.Preview(from, to);
        var inserted = _sync.CommitDraft(from, to);
        RefreshFromDraft();
        Status = inserted == 0
            ? preview.UnmatchedCount > 0 && preview.MatchedCount == 0
                ? "Nada se guardó: asigne las personas sin coincidencia o quite esos registros."
                : "Nada nuevo en este rango: esas marcaciones ya estaban en nómina (misma clave del reloj)."
            : $"Se guardaron {inserted} marcación(es) del rango. {preview.UnmatchedCount} sin persona se omitieron.";
    }

    partial void OnFromDateChanged(DateTimeOffset? value)
    {
        if (value is null)
        {
            FromDate = ToPicker(WeekRange.StartOfWeek(WeekRange.Today()));
            return;
        }

        ApplyDateFilter();
    }

    partial void OnToDateChanged(DateTimeOffset? value)
    {
        if (value is null)
        {
            ToDate = ToPicker(WeekRange.EndOfWeek(WeekRange.Today()));
            return;
        }

        ApplyDateFilter();
    }

    private void ApplyDateFilter()
    {
        RefreshFromDraft();
        if (_draft.HasItems)
        {
            Status = RangeStatus($"{_draft.Items.Count} leídas.");
        }
    }

    partial void OnIsBusyChanged(bool value) =>
        CanSave = CurrentPreview().MatchedCount > 0 && !value;

    partial void OnSelectedChanged(DeviceLogRow? value)
    {
        HasSelection = value is not null;
        if (value is null)
        {
            return;
        }

        EditDate = new DateTimeOffset(DateTime.SpecifyKind(value.PunchedAt, DateTimeKind.Local));
        EditTime = value.PunchedAt.TimeOfDay;
        AssignEmployee = Employees.FirstOrDefault(employee => employee.Id == value.Item.EmployeeId);
    }

    private void ReloadEmployees()
    {
        Employees.Clear();
        foreach (var employee in _store.GetEmployees(activeOnly: true))
        {
            Employees.Add(employee);
        }
    }

    private void RefreshFromDraft(DeviceLogDraftItem? keepSelection = null)
    {
        var selectedKey = keepSelection?.DeviceKey ?? Selected?.Item.DeviceKey;
        var selectedNumber = keepSelection?.RecordNumber ?? Selected?.RecordNumber;

        var (from, to) = CurrentRange();
        var visible = _draft.ItemsInRange(from, to);
        Logs.Clear();
        foreach (var item in visible)
        {
            Logs.Add(new DeviceLogRow(item));
        }

        var preview = _draft.Preview(from, to);
        ProposedShifts.Clear();
        foreach (var shift in preview.Shifts)
        {
            ProposedShifts.Add(shift);
        }

        Issues.Clear();
        foreach (var issue in preview.Issues)
        {
            Issues.Add(issue);
        }

        HasIssues = preview.Issues.Count > 0;
        CanSave = preview.MatchedCount > 0 && !IsBusy;
        PreviewSummary = PreviewLabel(preview, visible.Count);

        Selected = Logs.FirstOrDefault(row =>
            selectedKey is not null && row.Item.DeviceKey == selectedKey
            || row.RecordNumber == selectedNumber);
    }

    private DeviceLogPreview CurrentPreview()
    {
        var (from, to) = CurrentRange();
        return _draft.Preview(from, to);
    }

    private (DateOnly? From, DateOnly? To) CurrentRange()
    {
        DateOnly? from = FromDate is { } start ? DateOnly.FromDateTime(start.LocalDateTime) : null;
        DateOnly? to = ToDate is { } end ? DateOnly.FromDateTime(end.LocalDateTime) : null;
        return (from, to);
    }

    private bool InCurrentRange(DeviceLogDraftItem item)
    {
        var (from, to) = CurrentRange();
        return from is { } start && to is { } end && DeviceLogDraft.IsInRange(item.PunchedAt, start, end);
    }

    private string RangeStatus(string prefix)
    {
        var (from, to) = CurrentRange();
        if (from is { } start && to is { } end && end < start)
        {
            return $"{prefix} Desde no puede ser posterior a Hasta.";
        }

        var visible = _draft.ItemsInRange(from, to).Count;
        var outside = _draft.Items.Count - visible;
        var range = from is { } a && to is { } b
            ? $" · {visible} en {a:dd MMM} – {b:dd MMM}"
            : string.Empty;
        var extra = outside > 0 ? $" · {outside} fuera del rango" : string.Empty;
        return $"{prefix}{range}{extra} Revise, quite duplicados y luego guarde. Estado/Método son del reloj y no se usan para pagar.";
    }

    private string PreviewLabel(DeviceLogPreview preview, int visibleCount)
    {
        var (from, to) = CurrentRange();
        if (from is { } start && to is { } end && end < start)
        {
            return "Desde no puede ser posterior a Hasta.";
        }

        if (visibleCount == 0)
        {
            return _draft.Items.Count == 0
                ? "Sin eventos en la vista previa."
                : $"Sin eventos en este rango. Hay {_draft.Items.Count} registro(s) fuera de estas fechas.";
        }

        return $"{preview.MatchedCount} con persona · {preview.UnmatchedCount} sin persona · {preview.Shifts.Count} jornada(s) · {preview.TotalHours:0.##} h";
    }

    private static DateTimeOffset ToPicker(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue));
}

public sealed class DeviceLogRow
{
    public DeviceLogRow(DeviceLogDraftItem item)
    {
        Item = item;
    }

    public DeviceLogDraftItem Item { get; }
    public int RecordNumber => Item.RecordNumber;
    public string DeviceUserId => Item.DeviceUserId;
    public string EmployeeName => Item.EmployeeName;
    public bool IsMatched => Item.IsMatched;
    public string MatchLabel => Item.MatchLabel;
    public DateTime PunchedAt => Item.PunchedAt;
    public string VerifyModeLabel => Item.VerifyModeLabel;
    public string StatusLabel => Item.StatusLabel;
}
