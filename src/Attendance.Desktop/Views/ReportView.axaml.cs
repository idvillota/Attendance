using Attendance.Desktop.Services;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();
    }

    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReportViewModel vm || !vm.CanManagePayroll)
        {
            return;
        }

        var path = await DesktopDialogs.SaveFileAsync(
            this,
            "Exportar reporte semanal",
            "nomina-semanal.xlsx",
            "Excel",
            "*.xlsx");

        if (path is not null)
        {
            vm.ExportTo(path);
        }
    }
}
