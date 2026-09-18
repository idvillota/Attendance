using Attendance.Core.Domain;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();
    }

    private void PaintOrdinaryDay(object? sender, RoutedEventArgs e) => SetPaint(CalendarBand.OrdinaryDay);
    private void PaintOrdinaryNight(object? sender, RoutedEventArgs e) => SetPaint(CalendarBand.OrdinaryNight);
    private void PaintSundayDay(object? sender, RoutedEventArgs e) => SetPaint(CalendarBand.SundayDay);
    private void PaintSundayNight(object? sender, RoutedEventArgs e) => SetPaint(CalendarBand.SundayNight);

    private void SetPaint(CalendarBand band)
    {
        if (DataContext is ScheduleViewModel vm)
        {
            vm.PaintBand = band;
        }
    }

    private void CellClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ScheduleHourCell cell }
            && DataContext is ScheduleViewModel vm)
        {
            vm.PaintCommand.Execute(cell);
        }
    }
}
