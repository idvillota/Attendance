using System.Collections.ObjectModel;
using System.Globalization;
using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using Attendance.Core.Export;
using Attendance.Core.Payroll;
using Attendance.Desktop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class ReportViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IWeeklyPayrollService _payroll;
    private readonly IPayrollExporter _exporter;
    private readonly IAttendanceSyncService _sync;
    private readonly IAuthSession _session;
    private readonly IAppNavigator _navigator;

    public ReportViewModel(
        IAttendanceStore store,
        IWeeklyPayrollService payroll,
        IPayrollExporter exporter,
        IAttendanceSyncService sync,
        IAuthSession session,
        IAppNavigator navigator)
    {
        _store = store;
        _payroll = payroll;
        _exporter = exporter;
        _sync = sync;
        _session = session;
        _navigator = navigator;
        CanManagePayroll = session.IsAdmin;
        WeekStart = WeekRange.StartOfWeek(WeekRange.Today());
        Reload();
    }

    public ObservableCollection<WeeklyReportLine> Lines { get; } = [];
    public ObservableCollection<PunchIssue> Issues { get; } = [];
    public ObservableCollection<EmployeePresenceItem> PresenceItems { get; } = [];
    public bool CanManagePayroll { get; }

    [ObservableProperty]
    private DateTimeOffset _weekStartDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string _weekLabel = string.Empty;

    [ObservableProperty]
    private string _peopleCountText = string.Empty;

    [ObservableProperty]
    private string _totalHoursText = string.Empty;

    [ObservableProperty]
    private string _totalPayText = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _hasIssues;

    [ObservableProperty]
    private string _presenceFreshness = string.Empty;

    private DateOnly WeekStart { get; set; }
    private WeeklyReport? _report;

    [RelayCommand]
    public void Reload()
    {
        WeekStart = WeekRange.StartOfWeek(DateOnly.FromDateTime(WeekStartDate.LocalDateTime.Date));
        WeekStartDate = new DateTimeOffset(WeekStart.ToDateTime(TimeOnly.MinValue));
        _report = _payroll.Build(WeekStart, _store.GetEmployees(), _store.GetRoles(), _store.GetPunches(), _store.GetSchedule());

        Lines.Clear();
        foreach (var line in _report.Lines)
        {
            Lines.Add(line);
        }

        Issues.Clear();
        foreach (var issue in _report.Issues)
        {
            Issues.Add(issue);
        }

        WeekLabel = $"Semana del {_report.WeekStart:dd/MM/yyyy} al {_report.WeekEnd:dd/MM/yyyy}";
        PeopleCountText = $"{_report.Lines.Count}";
        TotalHoursText = $"{_report.TotalHours:0.##}";
        TotalPayText = _report.TotalPay.ToString("C0", CultureInfo.GetCultureInfo("es-CO"));
        HasIssues = _report.Issues.Count > 0;
        Status = _report.Issues.Count == 0
            ? "Todas las jornadas están cerradas. Ya puede exportar."
            : $"{_report.Issues.Count} marcación(es) incompletas. Corrija antes de pagar.";

        PresenceItems.Clear();
        foreach (var status in Presence.Snapshot(_store.GetEmployees(), _store.GetPunches()))
        {
            PresenceItems.Add(new EmployeePresenceItem(status));
        }

        PresenceFreshness = PresenceStatus.Freshness(_store.GetSettings().LastDownloadAt);
    }

    [RelayCommand]
    private void PreviousWeek()
    {
        WeekStartDate = WeekStartDate.AddDays(-7);
        Reload();
    }

    [RelayCommand]
    private void NextWeek()
    {
        WeekStartDate = WeekStartDate.AddDays(7);
        Reload();
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (!_session.IsAdmin)
        {
            return;
        }
        try
        {
            var (device, count) = await _sync.PullToDraftAsync(_store.GetSettings());
            Status = count == 0
                ? $"Sin marcaciones en {device.Name}."
                : $"{count} marcación(es) de {device.Name} listas para revisar en Logs K30.";
            _navigator.GoToDeviceLogs();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void ExportTo(string path)
    {
        if (!_session.IsAdmin)
        {
            return;
        }
        if (_report is null)
        {
            Reload();
        }

        _exporter.Export(_report!, path);
        Status = "Reporte Excel guardado.";
    }
}
