using Attendance.Core;
using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Export;
using Attendance.Core.Payroll;
using Attendance.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Desktop;

internal static class DesktopComposition
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AttendanceDb>(_ =>
        {
            var db = new AttendanceDb(AppPaths.DatabaseFile);
            db.Initialize();
            return db;
        });
        services.AddSingleton<IUserStore>(sp => sp.GetRequiredService<AttendanceDb>());
        services.AddSingleton<IAuthSession, AuthSession>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAttendanceStore>(sp =>
            new AuthorizedAttendanceStore(
                sp.GetRequiredService<AttendanceDb>(),
                sp.GetRequiredService<IAuthSession>()));
        services.AddSingleton<IWeeklyPayrollService, WeeklyPayrollService>();
        services.AddSingleton<IAttendanceSyncService, AttendanceSyncService>();
        services.AddSingleton<IPayrollExporter, PayrollExcelExporter>();
        services.AddSingleton<DesktopShell>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<RolesViewModel>();
        services.AddTransient<EmployeesViewModel>();
        services.AddTransient<PunchesViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<DeviceViewModel>();
        services.AddTransient<ScheduleViewModel>();
        services.AddTransient<UsersViewModel>();
        return services.BuildServiceProvider();
    }
}
