using System.Collections.ObjectModel;
using System.Globalization;
using Attendance.Core.Data;
using Attendance.Core.Domain;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class ScheduleViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;

    public ScheduleViewModel(IAttendanceStore store)
    {
        _store = store;
        Reload();
    }

    public ObservableCollection<ScheduleDayRow> Days { get; } = [];
    public IReadOnlyList<string> HourLabels { get; } = Enumerable.Range(0, 24).Select(h => h.ToString("00")).ToList();

    [ObservableProperty] private CalendarBand _paintBand = CalendarBand.OrdinaryNight;
    [ObservableProperty] private string _nightStartText = "19:00";
    [ObservableProperty] private string _nightEndText = "06:00";
    [ObservableProperty] private string _extraAfterText = "8";
    [ObservableProperty] private string _holidaysText = string.Empty;
    [ObservableProperty] private string _status = "Pinte las horas. Los pesos se editan en cada rol.";

    public void Reload()
    {
        var schedule = _store.GetSchedule();
        NightStartText = schedule.NightStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        NightEndText = schedule.NightEnd.ToString("HH:mm", CultureInfo.InvariantCulture);
        ExtraAfterText = schedule.ExtraAfterHours.ToString("0.##", CultureInfo.InvariantCulture);
        HolidaysText = string.Join(", ", schedule.Holidays.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd")));
        RebuildDays(schedule);
    }

    [RelayCommand]
    private void Paint(ScheduleHourCell? cell)
    {
        if (cell is null)
        {
            return;
        }

        cell.Band = PaintBand;
    }

    [RelayCommand]
    private void ApplyNightWindow()
    {
        if (!TryBuildSchedule(out var schedule, out var error))
        {
            Status = error!;
            return;
        }

        schedule.PaintFromNightWindow();
        RebuildDays(schedule);
        Status = "Horario pintado con noche y domingo. Pulse Guardar.";
    }

    [RelayCommand]
    private void Save()
    {
        if (!TryBuildSchedule(out var schedule, out var error))
        {
            Status = error!;
            return;
        }

        for (var d = 0; d < 7; d++)
        {
            for (var h = 0; h < 24; h++)
            {
                schedule.Cells[d * 24 + h] = Days[d].Hours[h].Band;
            }
        }

        _store.SaveSchedule(schedule);
        Status = "Horario guardado. El reporte semanal ya usa estas franjas.";
    }

    private bool TryBuildSchedule(out TenantSchedule schedule, out string? error)
    {
        schedule = TenantSchedule.Default();
        error = null;
        if (!TimeOnly.TryParse(NightStartText, out var start) || !TimeOnly.TryParse(NightEndText, out var end))
        {
            error = "Hora de noche inválida (use 19:00).";
            return false;
        }

        if (!decimal.TryParse(ExtraAfterText, NumberStyles.Number, CultureInfo.InvariantCulture, out var extra) || extra < 0)
        {
            error = "Horas extra: use un número (8, o 0 para no usar extras).";
            return false;
        }

        schedule.NightStart = start;
        schedule.NightEnd = end;
        schedule.ExtraAfterHours = extra;
        schedule.Holidays.Clear();
        foreach (var token in HolidaysText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!DateOnly.TryParse(token, out var date))
            {
                error = $"Fecha de festivo inválida: {token}";
                return false;
            }

            schedule.Holidays.Add(date);
        }

        return true;
    }

    private void RebuildDays(TenantSchedule schedule)
    {
        Days.Clear();
        var names = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
        for (var d = 0; d < 7; d++)
        {
            var dow = (DayOfWeek)(((int)DayOfWeek.Monday + d) % 7);
            var row = new ScheduleDayRow { DayName = names[d] };
            for (var h = 0; h < 24; h++)
            {
                row.Hours.Add(new ScheduleHourCell
                {
                    Day = dow,
                    Hour = h,
                    Band = schedule.Cell(dow, h)
                });
            }

            Days.Add(row);
        }
    }
}

public sealed class ScheduleDayRow
{
    public string DayName { get; set; } = string.Empty;
    public ObservableCollection<ScheduleHourCell> Hours { get; } = [];
}

public partial class ScheduleHourCell : ObservableObject
{
    public DayOfWeek Day { get; init; }
    public int Hour { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fill))]
    private CalendarBand _band;

    public IBrush Fill => Band switch
    {
        CalendarBand.OrdinaryDay => new SolidColorBrush(Color.Parse("#E4E7EC")),
        CalendarBand.OrdinaryNight => new SolidColorBrush(Color.Parse("#B9E6FE")),
        CalendarBand.SundayDay => new SolidColorBrush(Color.Parse("#FEDF89")),
        CalendarBand.SundayNight => new SolidColorBrush(Color.Parse("#FECDCA")),
        _ => Brushes.LightGray
    };
}
