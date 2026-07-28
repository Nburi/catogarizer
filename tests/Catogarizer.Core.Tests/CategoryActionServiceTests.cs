using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;
using Xunit;

namespace Catogarizer.Core.Tests;

public class CategoryActionServiceTests
{
    [Fact]
    public async Task OpenAsync_LaunchesAndPositionsAppsNotRunning()
    {
        var launcher = new FakeProcessLauncher();
        var windowManager = new FakeWindowManager();
        var service = new CategoryActionService(launcher, windowManager);
        var app = new AppEntry
        {
            Name = "Editor",
            ExecutablePath = "editor.exe",
            Window = new WindowRect { X = 1, Y = 2, Width = 800, Height = 600 },
        };
        var category = new Category { Apps = { app } };

        var result = await service.OpenAsync(category);

        Assert.True(result.AllSucceeded);
        Assert.Contains(app.Id, launcher.LaunchedIds);
        var call = Assert.Single(windowManager.MoveResizeCalls);
        Assert.Equal(800, call.Rect.Width);
    }

    [Fact]
    public async Task OpenAsync_RepositionsAlreadyRunningApp_WithoutRelaunching()
    {
        var launcher = new FakeProcessLauncher();
        var app = new AppEntry { Name = "Editor", ExecutablePath = "editor.exe" };
        launcher.RunningApps[app.Id] = new LaunchedApp(1, 999, app.ExecutablePath);
        var windowManager = new FakeWindowManager();
        var service = new CategoryActionService(launcher, windowManager);
        var category = new Category { Apps = { app } };

        var result = await service.OpenAsync(category);

        Assert.True(result.AllSucceeded);
        Assert.DoesNotContain(app.Id, launcher.LaunchedIds);
        Assert.Contains((nint)999, windowManager.RestoreCalls);
        Assert.Single(windowManager.MoveResizeCalls);
    }

    [Fact]
    public async Task OpenAsync_RecordsFailure_WhenLaunchThrows()
    {
        var launcher = new FakeProcessLauncher { ThrowOnLaunch = _ => new FileNotFoundException("missing") };
        var windowManager = new FakeWindowManager();
        var service = new CategoryActionService(launcher, windowManager);
        var app = new AppEntry { Name = "Ghost", ExecutablePath = "ghost.exe" };
        var category = new Category { Apps = { app } };

        var result = await service.OpenAsync(category);

        Assert.False(result.AllSucceeded);
        var outcome = Assert.Single(result.Outcomes);
        Assert.False(outcome.Success);
        Assert.Contains("not found", outcome.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CloseAsync_OnlyClosesRunningApps()
    {
        var launcher = new FakeProcessLauncher();
        var runningApp = new AppEntry { Name = "Running", ExecutablePath = "running.exe" };
        var notRunningApp = new AppEntry { Name = "NotRunning", ExecutablePath = "gone.exe" };
        launcher.RunningApps[runningApp.Id] = new LaunchedApp(1, 555, runningApp.ExecutablePath);
        var windowManager = new FakeWindowManager();
        var service = new CategoryActionService(launcher, windowManager);
        var category = new Category { Apps = { runningApp, notRunningApp } };

        var result = await service.CloseAsync(category);

        var closeCall = Assert.Single(windowManager.CloseCalls);
        Assert.Equal((nint)555, closeCall);
        Assert.True(result.AllSucceeded);
    }

    [Fact]
    public async Task MinimizeAsync_OnlyMinimizesRunningApps()
    {
        var launcher = new FakeProcessLauncher();
        var runningApp = new AppEntry { Name = "Running", ExecutablePath = "running.exe" };
        launcher.RunningApps[runningApp.Id] = new LaunchedApp(1, 321, runningApp.ExecutablePath);
        var windowManager = new FakeWindowManager();
        var service = new CategoryActionService(launcher, windowManager);
        var category = new Category { Apps = { runningApp } };

        await service.MinimizeAsync(category);

        var minimizeCall = Assert.Single(windowManager.MinimizeCalls);
        Assert.Equal((nint)321, minimizeCall);
    }

    [Fact]
    public async Task CloseAsync_RecordsFailure_WhenWindowNeverActuallyCloses()
    {
        var launcher = new FakeProcessLauncher();
        var app = new AppEntry { Name = "TrayApp", ExecutablePath = "tray.exe" };
        launcher.RunningApps[app.Id] = new LaunchedApp(1, 555, app.ExecutablePath);
        var windowManager = new FakeWindowManager { CloseSucceeds = false };
        var service = new CategoryActionService(launcher, windowManager);
        var category = new Category { Apps = { app } };

        var result = await service.CloseAsync(category);

        Assert.False(result.AllSucceeded);
        var outcome = Assert.Single(result.Outcomes);
        Assert.False(outcome.Success);
        Assert.Contains("TrayApp", outcome.ErrorMessage);
    }

    [Fact]
    public async Task MinimizeAsync_RecordsFailure_WhenWindowNeverActuallyMinimizes()
    {
        var launcher = new FakeProcessLauncher();
        var app = new AppEntry { Name = "Stubborn", ExecutablePath = "stubborn.exe" };
        launcher.RunningApps[app.Id] = new LaunchedApp(1, 321, app.ExecutablePath);
        var windowManager = new FakeWindowManager { MinimizeSucceeds = false };
        var service = new CategoryActionService(launcher, windowManager);
        var category = new Category { Apps = { app } };

        var result = await service.MinimizeAsync(category);

        Assert.False(result.AllSucceeded);
        var outcome = Assert.Single(result.Outcomes);
        Assert.False(outcome.Success);
        Assert.Contains("Stubborn", outcome.ErrorMessage);
    }
}
