using Attendance.Core.Domain;
using Attendance.Desktop.Services;
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
        if (sender is Control { DataContext: EmployeePresenceItem item })
        {
            await OpenEditor(item.Employee);
        }
    }

    private async void DeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EmployeesViewModel vm ||
            sender is not Control { DataContext: EmployeePresenceItem { Employee: { } employee } })
        {
            return;
        }

        await ConfirmDeleteAsync(this, vm, employee);
    }

    private async Task OpenEditor(Employee? employee)
    {
        if (DataContext is not EmployeesViewModel vm)
        {
            return;
        }

        if (!vm.CanManagePeople)
        {
            return;
        }

        var editor = vm.CreateEditor(employee);
        if (await DesktopDialogs.ShowEditorAsync<EmployeeEditorWindow>(this, editor))
        {
            vm.Reload(string.IsNullOrWhiteSpace(editor.DeleteStatus) ? null : editor.DeleteStatus);
        }
    }

    internal static async Task ConfirmDeleteAsync(Control host, EmployeesViewModel vm, Employee employee)
    {
        if (!vm.CanManagePeople)
        {
            return;
        }

        try
        {
            var delete = vm.CreateDelete(employee);
            if (await DesktopDialogs.ShowEditorAsync<EmployeeDeleteWindow>(host, delete))
            {
                vm.Reload(delete.ResultStatus);
            }
        }
        catch (Exception ex)
        {
            vm.Reload(ex.Message);
        }
    }
}
