using Attendance.Core.Domain;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
    }

    private async void AddClick(object? sender, RoutedEventArgs e) => await OpenEditor(null);

    private async void EditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Employee employee })
        {
            await OpenEditor(employee);
        }
    }

    private async Task OpenEditor(Employee? employee)
    {
        if (DataContext is not EmployeesViewModel vm)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new EmployeeEditorWindow
        {
            DataContext = vm.CreateEditor(employee)
        };

        if (await dialog.ShowDialog<bool>(owner))
        {
            vm.Reload();
        }
    }
}
