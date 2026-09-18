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

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EmployeeEditorViewModel vm && vm.TrySave())
        {
            Close(true);
        }
    }
}
