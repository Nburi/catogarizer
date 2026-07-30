using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.App.Views;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Cli;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;
using Catogarizer.Win32;

namespace Catogarizer.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Catogarizer.SingleInstance.9F3B2E7A";
    private const string ShowSignalEventName = "Catogarizer.ShowSignal.9F3B2E7A";
    private const string TriggerRunPipeName = "Catogarizer.TriggerRun.9F3B2E7A";

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private EventWaitHandle? _showSignalEvent;
    private GlobalHotkeyService? _hotkeyService;
    private TrayIconController? _trayIconController;
    private LibraryService? _library;
    private ICategoryActionService? _categoryActionService;
    private IAppBlockingService? _appBlockingService;
    private TriggerSchedulerService? _triggerScheduler;
    private CommandPaletteWindow? _paletteWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var command = CliCommand.Parse(e.Args);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        // initiallyOwned only grants ownership when this call is the one that creates the
        // mutex - a second instance gets a handle to the existing one but never actually
        // owns it, so it must never call ReleaseMutex (that throws).
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            if (command is CliCommand.Run run)
            {
                // "catogarizer run <name>" while the app is already open - relay the request
                // over the named pipe instead of the show-signal, so this stays headless/
                // scriptable rather than popping the main window.
                SendTriggerRunRequest(run.TriggerName);
            }
            else
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

        var triggerRunner = new TriggerRunner(categoryActionService);
        var triggerScheduler = new TriggerSchedulerService(new SystemClock(), trigger => Task.Run(() => triggerRunner.Run(trigger, library)));
        triggerScheduler.Start(() => library.Triggers);
        _triggerScheduler = triggerScheduler;

        var mainWindow = new MainWindow(library, installedAppFinder, dialogService, processLauncher, windowFinder,
            windowManager, monitorService, categoryActionService, appBlockingService, autostartService, triggerRunner, RegisterGlobalHotkey);
        // An explicit "run <name>" launch stays headless regardless of the StartMinimized
        // setting - it's a background request (script/hotkey tool), not a user opening the app.
        if (!library.Settings.StartMinimized && command is not CliCommand.Run)
            mainWindow.Show();

        _trayIconController = new TrayIconController(library, categoryActionService, appBlockingService, autostartService, mainWindow);

        RegisterGlobalHotkey(library.Settings.CommandPaletteHotkey);
        ListenForShowSignal();
        ListenForTriggerRunRequests(triggerRunner, library);
        DispatchCliCommand(command, triggerRunner, library);
    }

    private static void DispatchCliCommand(CliCommand command, TriggerRunner triggerRunner, LibraryService library)
    {
        switch (command)
        {
            case CliCommand.Start:
                Task.Run(() =>
                {
                    foreach (var trigger in library.Triggers.Where(t => t.IsEnabled && t.Type == TriggerType.Startup))
                        triggerRunner.Run(trigger, library);
                });
                break;
            case CliCommand.Run run:
                Task.Run(() =>
                {
                    var trigger = library.Triggers.FirstOrDefault(t => string.Equals(t.Name, run.TriggerName, StringComparison.OrdinalIgnoreCase));
                    if (trigger is not null) triggerRunner.Run(trigger, library);
                });
                break;
        }
    }

    /// <summary>
    /// Background listener for "catogarizer run &lt;name&gt;" requests relayed from a second
    /// process invocation - mirrors ListenForShowSignal's always-on background thread, but
    /// carries a payload (the trigger name) so it uses a named pipe instead of a bare event.
    /// </summary>
    private void ListenForTriggerRunRequests(TriggerRunner triggerRunner, LibraryService library)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(TriggerRunPipeName, PipeDirection.In);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var triggerName = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(triggerName))
                    {
                        var trigger = library.Triggers.FirstOrDefault(t => string.Equals(t.Name, triggerName, StringComparison.OrdinalIgnoreCase));
                        if (trigger is not null) triggerRunner.Run(trigger, library);
                    }
                }
                catch
                {
                    // Client disconnected early or the pipe broke - just loop and accept the
                    // next connection rather than killing the listener thread over it.
                }
            }
        })
        {
            IsBackground = true,
            Name = "Catogarizer-TriggerRunListener",
        };
        thread.Start();
    }

    private static void SendTriggerRunRequest(string triggerName)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", TriggerRunPipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(triggerName);
        }
        catch
        {
            // The running instance's pipe listener isn't up yet, or the connection otherwise
            // failed - there's no console to report that to, so the request is simply dropped.
        }
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
        _triggerScheduler?.Dispose();
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
