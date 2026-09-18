using Attendance.Core;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Export;
using Attendance.Core.Payroll;
using Attendance.Desktop.ViewModels;
using Attendance.Desktop.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AttendanceDb(AppPaths.DatabaseFile));
        services.AddSingleton<AttendanceSyncService>();
        services.AddSingleton<WeeklyPayrollService>();
        services.AddSingleton<PayrollExcelExporter>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<RolesViewModel>();
        services.AddSingleton<EmployeesViewModel>();
        services.AddSingleton<PunchesViewModel>();
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<DeviceViewModel>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<AttendanceDb>().Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
