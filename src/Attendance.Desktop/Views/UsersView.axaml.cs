using Attendance.Core.Domain;
using Attendance.Desktop.Services;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Attendance.Desktop.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
    }

    private async void AddClick(object? sender, RoutedEventArgs e) => await OpenEditor(null);

    private async void EditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AppUser user })
        {
            await OpenEditor(user);
        }
    }

    private async Task OpenEditor(AppUser? user)
    {
        if (DataContext is not UsersViewModel vm)
        {
            return;
        }

        if (await DesktopDialogs.ShowEditorAsync<UserEditorWindow>(this, vm.CreateEditor(user)))
        {
            vm.Reload();
        }
    }
}
