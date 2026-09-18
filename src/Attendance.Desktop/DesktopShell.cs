using Attendance.Core.Auth;
using Attendance.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Desktop;

internal sealed class DesktopShell
{
    private readonly IServiceProvider _provider;
    private readonly IAuthSession _session;

    public DesktopShell(IServiceProvider provider, IAuthSession session)
    {
        _provider = provider;
        _session = session;
    }

    public void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.MainWindow = CreateLogin(desktop);
    }

    private Window CreateLogin(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _session.SignOut();
        var vm = _provider.GetRequiredService<LoginViewModel>();
        var login = new Views.LoginWindow { DataContext = vm };
        vm.Succeeded += () => OpenMain(desktop, login);
        return login;
    }

    private void OpenMain(IClassicDesktopStyleApplicationLifetime desktop, Window login)
    {
        var vm = _provider.GetRequiredService<MainViewModel>();
        var main = new Views.MainWindow { DataContext = vm };
        vm.LogoutRequested += () => OpenLogin(desktop, main);
        desktop.MainWindow = main;
        main.Show();
        login.Close();
    }

    private void OpenLogin(IClassicDesktopStyleApplicationLifetime desktop, Window main)
    {
        var login = CreateLogin(desktop);
        desktop.MainWindow = login;
        login.Show();
        main.Close();
    }
}
