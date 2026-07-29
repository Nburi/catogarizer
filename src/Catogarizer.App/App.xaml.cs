using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Catogarizer.App.Services;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;
using Catogarizer.Win32;

namespace Catogarizer.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Catogarizer.SingleInstance.9F3B2E7A";

    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Deliberately a native MessageBox, not a themed dialog: this runs before any
            // themed window exists, and it's the one place a plain, always-works fallback
            // beats a custom dialog that depends on the app having started up correctly.
            MessageBox.Show(
                "Catogarizer is already running. Check your system tray.",
                "Catogarizer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var configStore = new JsonConfigStore(ConfigFilePath());
        var library = new LibraryService(configStore);
        var installedAppFinder = new InstalledAppFinder();
        var dialogService = new DialogService();

        var mainWindow = new MainWindow(library, installedAppFinder, dialogService);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static string ConfigFilePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catogarizer");
        return Path.Combine(dir, "config.json");
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Same reasoning as above: last-resort safety net, kept deliberately native/simple
        // rather than routed through the app's own (possibly-broken) UI.
        MessageBox.Show(
            $"Catogarizer ran into a problem and needs to close:\n\n{e.Exception.Message}",
            "Catogarizer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown();
    }
}
