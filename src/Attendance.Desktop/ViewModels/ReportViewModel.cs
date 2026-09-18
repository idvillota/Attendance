using System.Collections.ObjectModel;
using System.Globalization;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;
using Attendance.Core.Export;
using Attendance.Core.Payroll;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class ReportViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;
    private readonly WeeklyPayrollService _payroll;
    private readonly PayrollExcelExporter _exporter;
    private readonly AttendanceSyncService _sync;

    public ReportViewModel(
        AttendanceDb db,
        WeeklyPayrollService payroll,
        PayrollExcelExporter exporter,
        AttendanceSyncService sync)
    {
        _db = db;
        _payroll = payroll;
        _exporter = exporter;
        _sync = sync;
        WeekStart = WeekRange.StartOfWeek(WeekRange.Today());
        Reload();
    }

    public ObservableCollection<WeeklyReportLine> Lines { get; } = [];
    public ObservableCollection<PunchIssue> Issues { get; } = [];

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

    private DateOnly WeekStart { get; set; }
    private WeeklyReport? _report;

    [RelayCommand]
    public void Reload()
    {
        WeekStart = WeekRange.StartOfWeek(DateOnly.FromDateTime(WeekStartDate.LocalDateTime.Date));
        WeekStartDate = new DateTimeOffset(WeekStart.ToDateTime(TimeOnly.MinValue));
        _report = _payroll.Build(WeekStart, _db.GetEmployees(), _db.GetRoles(), _db.GetPunches());

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
        try
        {
            var settings = _db.GetSettings();
            var device = _sync.CreateDevice(settings);
            var logs = await device.DownloadLogsAsync();
            var count = _sync.Import(logs, settings.UseMockDevice ? PunchSource.Mock : PunchSource.Device);
            Reload();
            Status = count == 0
                ? $"Sin marcaciones nuevas desde {device.Name}."
                : $"Se importaron {count} marcación(es). Revise horas y anomalías.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void ExportTo(string path)
    {
        if (_report is null)
        {
            Reload();
        }

        _exporter.Export(_report!, path);
        Status = "Reporte Excel guardado.";
    }
}
