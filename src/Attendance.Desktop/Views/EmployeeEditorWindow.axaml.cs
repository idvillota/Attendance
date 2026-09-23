using Attendance.Desktop.Services;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class EmployeeEditorWindow : Window
{
    public EmployeeEditorWindow()
    {
        InitializeComponent();
    }

    private void CancelClick(object? sender, RoutedEventArgs e) =>
        Close(DataContext is EmployeeEditorViewModel { Saved: true });

    private void CloseClick(object? sender, RoutedEventArgs e) => Close(true);

    private async void SaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EmployeeEditorViewModel vm)
        {
            await vm.TrySaveAsync();
        }
    }

    private async void DeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EmployeeEditorViewModel { CanDelete: true } vm)
        {
            return;
        }

        try
        {
            var delete = vm.CreateDelete();
            if (await DesktopDialogs.ShowEditorAsync<EmployeeDeleteWindow>(this, delete))
            {
                vm.DeleteStatus = delete.ResultStatus;
                Close(true);
            }
        }
        catch (Exception)
        {
            // Preview failed; keep the editor open so the person can still be edited.
        }
    }
}
