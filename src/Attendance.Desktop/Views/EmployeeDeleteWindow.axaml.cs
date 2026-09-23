using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class EmployeeDeleteWindow : Window
{
    public EmployeeDeleteWindow()
    {
        InitializeComponent();
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void ConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EmployeeDeleteViewModel vm && await vm.TryDeleteAsync())
        {
            Close(true);
        }
    }
}
