using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public sealed class CategoryActionService : ICategoryActionService
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan CloseGracePeriod = TimeSpan.FromSeconds(2);

    private readonly IProcessLauncher _launcher;
    private readonly IWindowFinder _windowFinder;
    private readonly IWindowManager _windowManager;
    private readonly IMonitorService _monitorService;
    private readonly IDelay _delay;
    private readonly WindowPositioningService _positioning;
    private readonly Func<string, bool> _fileExists;

    public CategoryActionService(IProcessLauncher launcher, IWindowFinder windowFinder, IWindowManager windowManager,
        IMonitorService monitorService, IDelay delay, Func<string, bool>? fileExists = null)
    {
        _launcher = launcher;
        _windowFinder = windowFinder;
        _windowManager = windowManager;
        _monitorService = monitorService;
        _delay = delay;
        _positioning = new WindowPositioningService(windowManager, delay);
        _fileExists = fileExists ?? File.Exists;
    }

    public CategoryActionResult Open(IReadOnlyList<AppEntry> apps) => new(apps.Select(OpenApp).ToList());

    public AppActionResult OpenApp(AppEntry app)
    {
        if (!_fileExists(app.ExecutablePath))
            return Fail(app, $"\"{app.Name}\" wasn't found at {app.ExecutablePath}. It may have been moved or uninstalled. Use the pencil to point to the new location, or remove it.");

        try
        {
            var running = FindRunningWindows(app).FirstOrDefault()?.Handle;
            var hwnd = running ?? LaunchAndFind(app);
            if (hwnd is null)
                return Fail(app, $"\"{app.Name}\" didn't open a window in time.");

            // Already running: "open" means show it - a minimized window would otherwise stay invisible.
            if (running is { } existing)
                _windowManager.BringToFront(existing);

            if (app.Placement is not null)
                Position(hwnd.Value, app.Placement);

            return new AppActionResult(app, AppActionOutcome.Opened, WindowHandle: hwnd);
        }
        catch (Exception ex)
        {
            return Fail(app, $"Couldn't open \"{app.Name}\": {ex.Message}");
        }
    }

    public AppActionResult MinimizeApp(AppEntry app)
    {
        foreach (var window in FindRunningWindows(app))
            _windowManager.Minimize(window.Handle);
        return new AppActionResult(app, AppActionOutcome.Minimized);
    }

    public AppActionResult CloseApp(AppEntry app)
    {
        foreach (var window in FindRunningWindows(app))
        {
            _windowManager.CloseGraceful(window.Handle);
            _delay.Wait(CloseGracePeriod);
            if (_windowManager.IsWindowOpen(window.Handle))
                _windowManager.ForceKill(window.Handle);
        }
        return new AppActionResult(app, AppActionOutcome.Closed);
    }

    private IntPtr? LaunchAndFind(AppEntry app)
    {
        var pid = _launcher.Launch(app.ExecutablePath, app.Arguments);
        return _windowFinder.FindMainWindow(pid, [app.Name], LaunchTimeout);
    }

    private void Position(IntPtr hwnd, WindowRect placement)
    {
        var monitors = _monitorService.GetMonitors();
        var monitor = WindowPlacementResolver.ResolveMonitor(placement, monitors, _monitorService.GetPrimaryMonitor());
        var (x, y, width, height) = WindowPlacementResolver.ResolveAbsoluteRect(placement, monitor);
        _positioning.PositionWithRetry(hwnd, x, y, width, height);
    }

    private IReadOnlyList<OpenWindowInfo> FindRunningWindows(AppEntry app) =>
        _windowFinder.FindAllRunningWindows(AppWindowMatcher.ProcessNameCandidates(app), [app.Name]);

    private static AppActionResult Fail(AppEntry app, string message) => new(app, AppActionOutcome.Failed, message);
}
