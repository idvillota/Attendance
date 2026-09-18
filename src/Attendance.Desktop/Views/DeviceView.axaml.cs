using Attendance.Desktop.Services;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class DeviceView : UserControl
{
    public DeviceView()
    {
        InitializeComponent();
    }

    private async void ImportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceViewModel vm)
        {
            return;
        }

        var path = await DesktopDialogs.OpenFileAsync(
            this,
            "Importar marcaciones CSV",
            "CSV",
            "*.csv",
            "*.txt");

        if (path is not null)
        {
            vm.ImportCsv(path);
        }
    }
}
