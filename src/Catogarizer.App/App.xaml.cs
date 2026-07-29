using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.App.Views;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;
using Catogarizer.Win32;

namespace Catogarizer.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Catogarizer.SingleInstance.9F3B2E7A";
    private const string ShowSignalEventName = "Catogarizer.ShowSignal.9F3B2E7A";

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private EventWaitHandle? _showSignalEvent;
    private GlobalHotkeyService? _hotkeyService;
    private TrayIconController? _trayIconController;
    private LibraryService? _library;
    private ICategoryActionService? _categoryActionService;
    private IAppBlockingService? _appBlockingService;
    private CommandPaletteWindow? _paletteWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        // initiallyOwned only grants ownership when this call is the one that creates the
        // mutex - a second instance gets a handle to the existing one but never actually
        // owns it, so it must never call ReleaseMutex (that throws).
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            // Another instance is already running - ask it to show itself instead of
            // starting a second one.
            try
            {
                using var existingSignal = EventWaitHandle.OpenExisting(ShowSignalEventName);
                existingSignal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Deliberately a native MessageBox, not a themed dialog: this runs before any
                // themed window exists, and it's the one place a plain, always-works fallback
                // beats a custom dialog that depends on the app having started up correctly.
                MessageBox.Show(
                    "Catogarizer is already running. Check your system tray.",
                    "Catogarizer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var configStore = new JsonConfigStore(ConfigFilePath());
        var library = new LibraryService(configStore);
        var installedAppFinder = new InstalledAppFinder();
        var dialogService = new DialogService();
        var processLauncher = new ProcessLauncher();
        var windowFinder = new WindowFinder();
        var windowManager = new WindowManager();
        var monitorService = new MonitorService();
        var categoryActionService = new CategoryActionService(processLauncher, windowFinder, windowManager, monitorService, new SystemDelay());
        var autostartService = new AutostartService();
        var processWatcher = new ProcessWatcher();
        var appBlockingService = new AppBlockingService(processWatcher);
        appBlockingService.Start();
        _library = library;
        _categoryActionService = categoryActionService;
        _appBlockingService = appBlockingService;

        var mainWindow = new MainWindow(library, installedAppFinder, dialogService, processLauncher, windowFinder,
            windowManager, monitorService, categoryActionService, appBlockingService, autostartService, RegisterGlobalHotkey);
        if (!library.Settings.StartMinimized)
            mainWindow.Show();

        _trayIconController = new TrayIconController(library, categoryActionService, appBlockingService, autostartService, mainWindow);

        RegisterGlobalHotkey(library.Settings.CommandPaletteHotkey);
        ListenForShowSignal();
    }

    private void ListenForShowSignal()
    {
        _showSignalEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalEventName);
        var thread = new Thread(() =>
        {
            while (true)
            {
                _showSignalEvent.WaitOne();
                Dispatcher.Invoke(() => _trayIconController?.ShowMainWindow());
            }
        })
        {
            IsBackground = true,
            Name = "Catogarizer-ShowSignalListener",
        };
        thread.Start();
    }

    /// <summary>
    /// Called at startup and again whenever Settings saves a changed hotkey - reuses the
    /// same GlobalHotkeyService and subscribes the handler only once (Register() internally
    /// unregisters any previous combo), so re-calling this never creates a second background
    /// thread or double-fires the palette.
    /// </summary>
    private void RegisterGlobalHotkey(string hotkeyText)
    {
        if (!HotkeyStringParser.TryParse(hotkeyText, out var modifiers, out var vk))
            return; // invalid setting - silently skip rather than block startup over it

        if (_hotkeyService is null)
        {
            _hotkeyService = new GlobalHotkeyService();
            _hotkeyService.HotkeyPressed += () => Dispatcher.Invoke(ShowCommandPalette);
        }
        _hotkeyService.Register(modifiers, vk);
        // Registration can fail (combo claimed by another app); the app still works fine
        // without the fast-path palette, so this isn't treated as an error.
    }

    private void ShowCommandPalette()
    {
        if (_paletteWindow is { IsVisible: true })
        {
            _paletteWindow.Activate();
            return;
        }

        var vm = new CommandPaletteViewModel(_library!, _categoryActionService!, _appBlockingService!);
        _paletteWindow = new CommandPaletteWindow(vm);
        _paletteWindow.Show();
        _paletteWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconController?.Dispose();
        (_appBlockingService as IDisposable)?.Dispose();
        if (_ownsSingleInstanceMutex) _singleInstanceMutex?.ReleaseMutex();
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
