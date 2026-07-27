using System.Windows;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;
using Catogarizer.Win32;
using Microsoft.Extensions.DependencyInjection;

namespace Catogarizer.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<IConfigStore>(new JsonConfigStore());
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IAutostartManager, AutostartManager>();
        services.AddSingleton<IAppBlocker, AppBlocker>();
        services.AddSingleton<IWindowEnumerator, WindowEnumerator>();
        services.AddSingleton<IAppIconProvider, ShellIconProvider>();
        services.AddSingleton<ICategoryActionService, CategoryActionService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        if (e.Args.Contains("--minimized"))
            mainWindow.WindowState = WindowState.Minimized;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<IAppBlocker>()?.Stop();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
