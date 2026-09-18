using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Attendance.Desktop.Views;

public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();
    }

    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReportViewModel vm)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar reporte semanal",
            SuggestedFileName = "nomina-semanal.xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

        if (file is not null)
        {
            vm.ExportTo(file.Path.LocalPath);
        }
    }
}
