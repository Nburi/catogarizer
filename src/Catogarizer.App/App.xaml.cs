using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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
    private const string CliPipeName = "Catogarizer.Cli.9F3B2E7A";

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private EventWaitHandle? _showSignalEvent;
    private GlobalHotkeyService? _hotkeyService;
    private TrayIconController? _trayIconController;
    private LibraryService? _library;
    private IAppBlockingService? _appBlockingService;
    private TriggerSchedulerService? _triggerScheduler;
    private CategorySwitchService? _switchService;
    private CategorySwitcher? _switcher;
    private CommandPaletteWindow? _paletteWindow;
    private SwitchPillWindow? _pill;
    private readonly DoubleTapDetector _doubleTap = new();
    private bool _handlingHotkey;

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
            if (command.IsRelayed)
            {
                // "catogarizer switch/run/back" while the app is already open - relay it over the
                // named pipe instead of the show-signal, so this stays headless/scriptable rather
                // than popping the main window.
                SendCliRequest(command);
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
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreHiddenWindows();

        var configStore = new JsonConfigStore(DataFilePath("config.json"));
        var library = new LibraryService(configStore);
        var themeService = new ThemeService(Resources);
        themeService.Apply(library.Settings.Theme);
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
        _appBlockingService = appBlockingService;

        // Before anything can hide a window: shows whatever a crashed run left hidden, then
        // snapshots what's open into Unsorted.
        var switchService = new CategorySwitchService(windowManager, windowFinder, new WindowWatcher(windowFinder),
            categoryActionService, () => library.PinnedApps, new SystemClock(), new JsonHiddenWindowStore(DataFilePath("hidden.json")));
        switchService.Initialize();
        var switcher = new CategorySwitcher(library, switchService, appBlockingService);
        _switchService = switchService;
        _switcher = switcher;

        var triggerRunner = new TriggerRunner(categoryActionService, switcher.SwitchTo);
        var triggerScheduler = new TriggerSchedulerService(new SystemClock(),
            trigger => Task.Run(() => ReportFailures(trigger.Name, triggerRunner.Run(trigger, library))));
        triggerScheduler.Start(() => library.Triggers);
        _triggerScheduler = triggerScheduler;

        var mainWindow = new MainWindow(library, installedAppFinder, dialogService, processLauncher, windowFinder,
            windowManager, monitorService, categoryActionService, switcher, autostartService, triggerRunner, themeService, RegisterGlobalHotkey);
        // A relayed command (switch/run/back) stays headless regardless of the StartMinimized
        // setting - it's a background request (script/hotkey tool), not a user opening the app.
        if (!library.Settings.StartMinimized && !command.IsRelayed)
            mainWindow.Show();

        _trayIconController = new TrayIconController(library, switcher, appBlockingService, autostartService, mainWindow, OnSwitched);

        RegisterGlobalHotkey(library.Settings.CommandPaletteHotkey);
        ListenForShowSignal();
        ListenForCliRequests(triggerRunner);
        Task.Run(() => ExecuteCliCommand(command, triggerRunner));
    }

    private void ExecuteCliCommand(CliCommand command, TriggerRunner triggerRunner)
    {
        var library = _library!;
        var switcher = _switcher!;
        switch (command)
        {
            case CliCommand.Start:
                foreach (var trigger in library.Triggers.Where(t => t.IsEnabled && t.Type == TriggerType.Startup))
                    ReportFailures(trigger.Name, triggerRunner.Run(trigger, library));
                break;
            case CliCommand.Run run:
                var named = library.Triggers.FirstOrDefault(t => string.Equals(t.Name, run.TriggerName, StringComparison.OrdinalIgnoreCase));
                if (named is null) ShowProblem("Trigger not found", $"There is no trigger named \"{run.TriggerName}\".");
                else ReportFailures(named.Name, triggerRunner.Run(named, library));
                break;
            case CliCommand.Switch s:
                var result = switcher.SwitchByName(s.CategoryName);
                if (result is null) ShowProblem("Category not found", $"There is no category named \"{s.CategoryName}\".");
                else OnSwitched(result);
                break;
            case CliCommand.Back:
                if (switcher.SwitchBack() is { } back) OnSwitched(back);
                break;
        }
    }

    /// <summary>Launch problems from a switch the user started outside the main window.</summary>
    private void OnSwitched(SwitchResult result)
    {
        if (result.Failures.Count == 0) return;
        var name = _library!.Categories.FirstOrDefault(c => c.Id == result.CategoryId)?.Name ?? CategorySwitchService.UncategorizedName;
        ShowProblem($"{name}: not everything opened", MainViewModel.DescribeFailures(result.Failures)!);
    }

    private void ReportFailures(string triggerName, IReadOnlyList<AppActionResult> results)
    {
        var failures = results.Where(r => r.Outcome == AppActionOutcome.Failed).ToList();
        if (failures.Count > 0)
            ShowProblem($"Trigger \"{triggerName}\" had a problem", MainViewModel.DescribeFailures(failures)!);
    }

    private void ShowProblem(string title, string message) =>
        Dispatcher.BeginInvoke(() => _trayIconController?.ShowProblem(title, message));

    /// <summary>
    /// Background listener for "catogarizer switch/run/back" requests relayed from a second
    /// process invocation - mirrors ListenForShowSignal's always-on background thread, but
    /// carries a payload so it uses a named pipe instead of a bare event.
    /// </summary>
    private void ListenForCliRequests(TriggerRunner triggerRunner)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(CliPipeName, PipeDirection.In);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var command = CliCommand.FromPipeMessage(reader.ReadLine());
                    if (command.IsRelayed) ExecuteCliCommand(command, triggerRunner);
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
            Name = "Catogarizer-CliListener",
        };
        thread.Start();
    }

    private static void SendCliRequest(CliCommand command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", CliPipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(command.ToPipeMessage());
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
            // Tap timing is taken here, on the hotkey thread, so a busy UI thread can't stretch or
            // squash the gap. BeginInvoke (not Invoke) queues the taps in order instead of letting
            // the second one run nested inside the first one's palette Show().
            _hotkeyService.HotkeyPressed += () =>
            {
                var isDoubleTap = _doubleTap.RegisterTap();
                Dispatcher.BeginInvoke(() => OnHotkeyPressed(isDoubleTap));
            };
        }
        _hotkeyService.Register(modifiers, vk);
        // Registration can fail (combo claimed by another app); the app still works fine
        // without the fast-path palette, so this isn't treated as an error.
    }

    /// <summary>
    /// One tap toggles the palette right away (no waiting to see if a second tap follows - that
    /// would slow down the everyday path). A quick second tap dismisses the half-faded-in
    /// palette and goes back to the previous category instead.
    /// </summary>
    private void OnHotkeyPressed(bool isDoubleTap)
    {
        if (_handlingHotkey)
        {
            Dispatcher.BeginInvoke(() => OnHotkeyPressed(isDoubleTap), DispatcherPriority.Background);
            return;
        }

        _handlingHotkey = true;
        try
        {
            if (isDoubleTap)
            {
                GoBack();
                return;
            }

            if (_paletteWindow is { IsOpen: true })
            {
                _paletteWindow.Dismiss();
                return;
            }

            var vm = new CommandPaletteViewModel(_library!, _switcher!, OnSwitched);
            _paletteWindow = new CommandPaletteWindow(vm);
            _paletteWindow.Show();
            _paletteWindow.Activate();
        }
        finally
        {
            _handlingHotkey = false;
        }
    }

    private void GoBack()
    {
        _paletteWindow?.Dismiss();
        _pill ??= new SwitchPillWindow();

        if (_switcher!.PreviousCategoryId is not { } previous)
        {
            _pill.Flash("Nothing to go back to yet", null, showBackArrow: false);
            return;
        }

        _pill.Flash(CategoryName(previous), CategoryColor(previous), showBackArrow: true);
        Task.Run(() =>
        {
            if (_switcher.SwitchBack() is { } result) OnSwitched(result);
        });
    }

    private string CategoryName(Guid id) =>
        _library!.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? CategorySwitchService.UncategorizedName;

    private Color? CategoryColor(Guid id) =>
        id == CategorySwitchService.Uncategorized ? (Color)FindResource("MutedColor") : (Color)FindResource("AccentColor");

    /// <summary>Never leave a window hidden behind when Catogarizer stops (PRINCIPLES.md, value 1).</summary>
    private void RestoreHiddenWindows()
    {
        try { _switcher?.ShowAllAndReset(); }
        catch { /* shutting down anyway; hidden.json lets the next start finish the job */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RestoreHiddenWindows();
        _switchService?.Dispose();
        _hotkeyService?.Dispose();
        _trayIconController?.Dispose();
        _triggerScheduler?.Dispose();
        (_appBlockingService as IDisposable)?.Dispose();
        if (_ownsSingleInstanceMutex) _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static string DataFilePath(string fileName)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catogarizer");
        return Path.Combine(dir, fileName);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        RestoreHiddenWindows();
        // Same reasoning as above: last-resort safety net, kept deliberately native/simple
        // rather than routed through the app's own (possibly-broken) UI.
        MessageBox.Show(
            $"Catogarizer ran into a problem and needs to close. All windows it had hidden are visible again.\n\n{e.Exception.Message}",
            "Catogarizer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown();
    }
}
