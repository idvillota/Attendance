using Attendance.Core.Auth;
using Attendance.Desktop.ViewModels;
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
        var provider = DesktopComposition.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            provider.GetRequiredService<DesktopShell>().Start(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
