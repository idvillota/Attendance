using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

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

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar marcaciones CSV",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CSV") { Patterns = ["*.csv", "*.txt"] }]
        });

        if (files.Count > 0)
        {
            vm.ImportCsv(files[0].Path.LocalPath);
        }
    }
}
