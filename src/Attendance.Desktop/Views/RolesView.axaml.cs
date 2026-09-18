using Attendance.Core.Domain;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class RolesView : UserControl
{
    public RolesView()
    {
        InitializeComponent();
    }

    private async void AddClick(object? sender, RoutedEventArgs e) => await OpenEditor(null);

    private async void EditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Role role })
        {
            await OpenEditor(role);
        }
    }

    private async Task OpenEditor(Role? role)
    {
        if (DataContext is not RolesViewModel vm || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new RoleEditorWindow { DataContext = vm.CreateEditor(role) };
        if (await dialog.ShowDialog<bool>(owner))
        {
            vm.Reload();
        }
    }
}
