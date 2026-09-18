using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class RoleEditorWindow : Window
{
    public RoleEditorWindow()
    {
        InitializeComponent();
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RoleEditorViewModel vm && vm.TrySave())
        {
            Close(true);
        }
    }
}
