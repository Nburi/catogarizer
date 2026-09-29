using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class CategoryActionServiceTests
{
    private readonly FakeProcessLauncher _launcher = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeDelay _delay = new();
    private static readonly MonitorInfo Primary = new("\\\\.\\DISPLAY1", 0, 0, 1920, 1080, IsPrimary: true);

    private CategoryActionService CreateService(bool fileExists = true) =>
        new(_launcher, _windowFinder, _windowManager, new StubMonitorService(Primary), _delay, _ => fileExists);

    private static AppEntry MakeApp(string name = "VS Code", string path = @"C:\code.exe") =>
        new() { Name = name, ExecutablePath = path };

    [Fact]
    public void OpenApp_WhenExecutableIsMissing_FailsWithoutLaunching()
    {
        var service = CreateService(fileExists: false);
        var app = MakeApp();

        var result = service.OpenApp(app);

        Assert.Equal(AppActionOutcome.Failed, result.Outcome);
        Assert.Contains("wasn't found", result.ErrorMessage);
        Assert.Empty(_launcher.LaunchCalls);
    }

    [Fact]
    public void OpenApp_WhenAlreadyRunning_AdoptsItWithoutLaunching()
    {
        var service = CreateService();
        var app = MakeApp(path: @"C:\code.exe");
        var hwnd = new IntPtr(42);
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(hwnd, "VS Code", "code", 999));

        var result = service.OpenApp(app);

        Assert.Equal(AppActionOutcome.Opened, result.Outcome);
        Assert.Empty(_launcher.LaunchCalls);
    }

    [Fact]
    public void OpenApp_WhenAlreadyRunning_BringsItToFrontInsteadOfLaunching()
    {
        var service = CreateService();
        var app = MakeApp(path: @"C:\charmap.exe");
        var hwnd = new IntPtr(44);
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(hwnd, "Zeichentabelle", "charmap", 997));

        service.OpenApp(app);

        Assert.Contains(hwnd, _windowManager.BringToFrontCalls);
        Assert.Empty(_launcher.LaunchCalls);
    }

    [Fact]
    public void OpenApp_WhenNotRunning_LaunchesAndFindsWindow()
    {
        var service = CreateService();
        var app = MakeApp();
        var hwnd = new IntPtr(7);
        _launcher.NextPid = 555;
        _windowFinder.MainWindowByPid[555] = hwnd;

        var result = service.OpenApp(app);

        Assert.Equal(AppActionOutcome.Opened, result.Outcome);
        Assert.Single(_launcher.LaunchCalls);
        Assert.Equal(app.ExecutablePath, _launcher.LaunchCalls[0].ExecutablePath);
    }

    [Fact]
    public void OpenApp_WhenLaunchedButNoWindowAppears_Fails()
    {
        var service = CreateService();
        var app = MakeApp();
        // MainWindowByPid intentionally left empty -> FindMainWindow returns null.

        var result = service.OpenApp(app);

        Assert.Equal(AppActionOutcome.Failed, result.Outcome);
        Assert.Contains("didn't open a window", result.ErrorMessage);
    }

    [Fact]
    public void OpenApp_WithCapturedPlacement_PositionsTheWindow()
    {
        var service = CreateService();
        var app = MakeApp();
        app.Placement = new WindowRect { OffsetX = 50, OffsetY = 60, Width = 800, Height = 600, MonitorId = Primary.Id };
        var hwnd = new IntPtr(9);
        _launcher.NextPid = 200;
        _windowFinder.MainWindowByPid[200] = hwnd;

        service.OpenApp(app);

        Assert.True(_windowManager.PositionCallCount >= 1);
        var bounds = _windowManager.GetBounds(hwnd);
        Assert.Equal((50, 60, 800, 600), bounds);
    }

    [Fact]
    public void MinimizeApp_MinimizesEveryRunningWindowForTheApp()
    {
        var service = CreateService();
        var app = MakeApp(path: @"C:\code.exe");
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(1), "VS Code", "code", 1));
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(2), "VS Code - workspace2", "code", 2));

        var result = service.MinimizeApp(app);

        Assert.Equal(AppActionOutcome.Minimized, result.Outcome);
        Assert.Equal(2, _windowManager.MinimizeCalls.Count);
    }

    [Fact]
    public void MinimizeApp_ForProxyLaunchedPwa_MatchesTheRealBrowserProcess()
    {
        // A PWA's ExecutablePath is the msedge_proxy.exe/chrome_proxy.exe stub the
        // shortcut launches, but that stub exits immediately - the actual window is
        // owned by the plain "msedge"/"chrome" process, which must still match.
        var service = CreateService();
        var app = MakeApp(name: "YouTube Music", path: @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge_proxy.exe");
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(1), "YouTube Music", "msedge", 1));

        var result = service.MinimizeApp(app);

        Assert.Equal(AppActionOutcome.Minimized, result.Outcome);
        Assert.Single(_windowManager.MinimizeCalls);
    }

    [Fact]
    public void CloseApp_WhenGracefulCloseWorks_DoesNotForceKill()
    {
        _windowManager.GracefulCloseWorks = true;
        var service = CreateService();
        var app = MakeApp(path: @"C:\code.exe");
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(1), "VS Code", "code", 1));

        service.CloseApp(app);

        Assert.Single(_windowManager.CloseGracefulCalls);
        Assert.Empty(_windowManager.ForceKillCalls);
    }

    [Fact]
    public void CloseApp_WhenAppIgnoresGracefulClose_FallsBackToForceKill()
    {
        _windowManager.GracefulCloseWorks = false;
        var service = CreateService();
        var app = MakeApp(path: @"C:\code.exe");
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(1), "VS Code", "code", 1));

        service.CloseApp(app);

        Assert.Single(_windowManager.CloseGracefulCalls);
        Assert.Single(_windowManager.ForceKillCalls);
    }

    [Fact]
    public void Open_OnMultipleApps_AggregatesResultsAndKeepsGoingAfterAFailure()
    {
        var service = CreateService();
        var missing = MakeApp("Ghost", @"C:\ghost.exe");
        var real = MakeApp("VS Code", @"C:\code.exe");
        _launcher.NextPid = 300;
        _windowFinder.MainWindowByPid[300] = new IntPtr(3);

        var serviceWithSelectiveExists = new CategoryActionService(_launcher, _windowFinder, _windowManager,
            new StubMonitorService(Primary), _delay, path => path == real.ExecutablePath);

        var result = serviceWithSelectiveExists.Open([missing, real]);

        Assert.Equal(2, result.AppResults.Count);
        Assert.Single(result.Failures);
        Assert.Equal("Ghost", result.Failures[0].App.Name);
    }

    private sealed class StubMonitorService(MonitorInfo primary) : IMonitorService
    {
        public IReadOnlyList<MonitorInfo> GetMonitors() => [primary];
        public MonitorInfo GetPrimaryMonitor() => primary;
    }
}
