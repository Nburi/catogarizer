using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class AppBlockingServiceTests
{
    private readonly FakeProcessWatcher _watcher = new();
    private readonly List<int> _killedPids = new();
    private readonly AppBlockingService _service;

    public AppBlockingServiceTests()
    {
        _service = new AppBlockingService(_watcher, pid => _killedPids.Add(pid));
    }

    private static BlockedApp MakeBlocked(string name, string processNameOrPath) =>
        new() { Name = name, ProcessNameOrPath = processNameOrPath };

    [Fact]
    public void Start_StartsTheUnderlyingWatcher()
    {
        _service.Start();

        Assert.True(_watcher.IsStarted);
    }

    [Fact]
    public void ProcessStart_WhenNoCategoryIsActive_DoesNothing()
    {
        _watcher.RaiseProcessStarted(new RunningProcessInfo(123, "discord", @"C:\discord.exe"));

        Assert.Empty(_killedPids);
    }

    [Fact]
    public void ProcessStart_MatchingActiveCategoryBlocklistByProcessName_GetsKilled()
    {
        var categoryId = Guid.NewGuid();
        var blocked = MakeBlocked("Discord", "discord");
        _service.ActivateCategory(categoryId, [blocked]);

        string? blockedName = null;
        _service.AppBlocked += name => blockedName = name;

        _watcher.RaiseProcessStarted(new RunningProcessInfo(123, "discord", @"C:\Users\x\AppData\Local\Discord\Update.exe"));

        Assert.Equal([123], _killedPids);
        Assert.Equal("Discord", blockedName);
    }

    [Fact]
    public void ProcessStart_MatchingByFullPath_GetsKilled()
    {
        var categoryId = Guid.NewGuid();
        var blocked = MakeBlocked("Steam", @"C:\Steam\steam.exe");
        _service.ActivateCategory(categoryId, [blocked]);

        _watcher.RaiseProcessStarted(new RunningProcessInfo(456, "steam", @"C:\Steam\steam.exe"));

        Assert.Equal([456], _killedPids);
    }

    [Fact]
    public void ProcessStart_NonMatchingProcess_IsIgnored()
    {
        var categoryId = Guid.NewGuid();
        _service.ActivateCategory(categoryId, [MakeBlocked("Discord", "discord")]);

        _watcher.RaiseProcessStarted(new RunningProcessInfo(789, "notepad", @"C:\Windows\notepad.exe"));

        Assert.Empty(_killedPids);
    }

    [Fact]
    public void DeactivateCategory_StopsEnforcingItsBlocklist()
    {
        var categoryId = Guid.NewGuid();
        _service.ActivateCategory(categoryId, [MakeBlocked("Discord", "discord")]);
        _service.DeactivateCategory(categoryId);

        _watcher.RaiseProcessStarted(new RunningProcessInfo(123, "discord", null));

        Assert.Empty(_killedPids);
    }

    [Fact]
    public void ActivateCategory_UnionsBlocklistsAcrossMultipleActiveCategories()
    {
        _service.ActivateCategory(Guid.NewGuid(), [MakeBlocked("Discord", "discord")]);
        _service.ActivateCategory(Guid.NewGuid(), [MakeBlocked("Steam", "steam")]);

        _watcher.RaiseProcessStarted(new RunningProcessInfo(1, "discord", null));
        _watcher.RaiseProcessStarted(new RunningProcessInfo(2, "steam", null));

        Assert.Equal([1, 2], _killedPids);
    }

    [Fact]
    public void ProcessStart_HelperOfAnAlreadyRunningInstance_IsNotKilled()
    {
        var service = new AppBlockingService(_watcher, pid => _killedPids.Add(pid), pid => pid == 200 ? "chrome" : "explorer");
        service.ActivateCategory(Guid.NewGuid(), [MakeBlocked("Chrome", "chrome")]);

        _watcher.RaiseProcessStarted(new RunningProcessInfo(200, "chrome", @"C:\chrome.exe"));
        _watcher.RaiseProcessStarted(new RunningProcessInfo(300, "chrome", @"C:\chrome.exe"));

        Assert.Equal([300], _killedPids);
    }
}
