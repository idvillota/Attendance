using Attendance.Core;
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
        services.AddSingleton<IAttendanceStore>(sp => sp.GetRequiredService<AttendanceDb>());
        services.AddSingleton<IWeeklyPayrollService, WeeklyPayrollService>();
        services.AddSingleton<IAttendanceSyncService, AttendanceSyncService>();
        services.AddSingleton<IPayrollExporter, PayrollExcelExporter>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<RolesViewModel>();
        services.AddSingleton<EmployeesViewModel>();
        services.AddSingleton<PunchesViewModel>();
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<DeviceViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        return services.BuildServiceProvider();
    }
}
