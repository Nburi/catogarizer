using Catogarizer.Core.Models;
using Catogarizer.Core.Resources;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

/// <summary>Closing a category asks its windows to close - never the processes - and says what stayed open.</summary>
public sealed class CategoryCloseTests
{
    private readonly FakeConfigStore _store = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly FakeWindowWatcher _windowWatcher = new();
    private readonly FakeClock _clock = new();
    private readonly LibraryService _library;
    private readonly CategorySwitchService _switchService;
    private readonly CategorySwitcher _switcher;
    private readonly TemporaryCategoryService _temporary;
    private readonly CategoryCloseService _close;
    private readonly FakeDelay _delay = new();
    private readonly Category _work;
    private readonly Category _comms;

    public CategoryCloseTests()
    {
        _library = new LibraryService(_store, fileExists: _ => true);
        _switchService = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, new FakeCategoryActionService(),
            () => _library.PinnedApps, _clock, new FakeHiddenWindowStore(), ownProcessId: 4242);
        _switcher = new CategorySwitcher(_library, _switchService, new FakeAppBlockingService());
        _temporary = new TemporaryCategoryService(_library, _switcher, _windowManager, _clock);
        _close = new CategoryCloseService(_library, _switcher, _windowManager, _temporary, delay: _delay);
        _work = _library.AddCategory("Work");
        _comms = _library.AddCategory("Comms");
        _switchService.Initialize();
    }

    private static OpenWindowInfo Window(int handle) => new(new IntPtr(handle), $"Window {handle}", "app", handle);

    /// <summary>A window that appears while <paramref name="category"/> is active, so it joins that category.</summary>
    private void OpenIn(Category category, params int[] handles)
    {
        if (_switcher.ActiveCategoryId != category.Id) _switcher.SwitchTo(category.Id);
        foreach (var handle in handles) _windowWatcher.RaiseWindowAppeared(Window(handle));
    }

    private int WindowsIn(Guid category) => _switcher.GetSessions().TryGetValue(category, out var list) ? list.Count : 0;

    [Fact]
    public void Close_AsksEveryWindowToClose_NotTheirProcesses()
    {
        OpenIn(_comms, 1, 2, 3);
        _switcher.SwitchTo(_work.Id);

        var report = _close.Close(_comms.Id)!;

        Assert.Equal(3, report.Requested);
        Assert.Equal(3, report.Closed);
        Assert.Empty(report.StillOpen);
        Assert.Equal([1, 2, 3], _windowManager.CloseGracefulCalls.Select(h => h.ToInt32()).Order());
        Assert.Empty(_windowManager.ForceKillCalls);
        Assert.Equal(0, WindowsIn(_comms.Id));
    }

    [Fact]
    public void Close_AWindowThatDoesNotGoAway_IsReportedAndLeftWhereItIs()
    {
        OpenIn(_comms, 1, 2);
        _switcher.SwitchTo(_work.Id);
        _windowManager.GracefulCloseWorks = false;

        var report = _close.Close(_comms.Id)!;

        Assert.Equal(2, report.StillOpen.Count);
        Assert.Equal(0, report.Closed);
        Assert.Equal(2, WindowsIn(_comms.Id));
        Assert.Equal(_work.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Close_WhenSomeClose_ReportsHowManyOfHowMany()
    {
        OpenIn(_comms, 1, 2, 3);
        _switcher.SwitchTo(_work.Id);
        _windowManager.GracefulCloseWorks = false;
        // While Close waits, the user answers window 1's "Save?" prompt and it closes.
        _delay.OnWait = () => _windowManager.SimulateClosedExternally(new IntPtr(1));

        var report = _close.Close(_comms.Id)!;

        Assert.Equal(3, report.Requested);
        Assert.Equal(1, report.Closed);
        Assert.Equal([2, 3], report.StillOpen.Select(w => w.Handle.ToInt32()).Order());
    }

    [Fact]
    public void Close_TheActiveCategory_WhenAllWindowsGo_ReturnsToThePreviousOneThatHasWindows()
    {
        OpenIn(_work, 10);
        OpenIn(_comms, 1, 2);

        _close.Close(_comms.Id);

        Assert.Equal(_work.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Close_TheActiveCategory_WhenThePreviousOneHasNothingParked_LandsInUnsortedInsteadOfLaunchingAnything()
    {
        OpenIn(_comms, 1, 2);

        _close.Close(_comms.Id);

        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Close_TheActiveCategory_WhenSomeWindowStays_StaysThere()
    {
        OpenIn(_work, 10);
        OpenIn(_comms, 1, 2);
        _windowManager.GracefulCloseWorks = false;
        _windowManager.SimulateClosedExternally(new IntPtr(1));

        var report = _close.Close(_comms.Id)!;

        Assert.Single(report.StillOpen);
        Assert.Equal(_comms.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Close_UnsortedCannotBeClosedAsAWhole()
    {
        _windowFinder.RunningWindows.Add(Window(7));

        Assert.Null(_close.Close(CategorySwitchService.Uncategorized));
        Assert.Empty(_windowManager.CloseGracefulCalls);
    }

    [Fact]
    public void Close_ACategoryWithoutWindows_AsksForNothing()
    {
        var report = _close.Close(_comms.Id)!;

        Assert.Equal(0, report.Requested);
        Assert.Empty(report.StillOpen);
        Assert.Empty(_windowManager.CloseGracefulCalls);
    }

    [Fact]
    public void Close_AQuickCategory_GoesOnceItsLastWindowIsGone_EvenWhenParked()
    {
        var quick = _temporary.Start(null);
        _windowWatcher.RaiseWindowAppeared(Window(1));
        _temporary.Tick();
        _switcher.SwitchTo(_work.Id);

        var report = _close.Close(quick.Id)!;
        _clock.Now += TimeSpan.FromSeconds(1);
        _temporary.Tick();

        Assert.Equal(1, report.Closed);
        Assert.DoesNotContain(_library.Categories, c => c.Id == quick.Id);
    }

    [Fact]
    public void Close_WakesFrozenAppsFirst_SoTheyCanAnswer()
    {
        var table = new FakeProcessTable().Add(20, 1, "slack");
        var governor = new FakeProcessGovernor();
        var settings = new FakeResourceSettings();
        var sessions = new FakeWindowSessions { ActiveCategoryId = _work.Id };
        sessions.Add(_comms.Id, 20, "slack");
        settings.Modes[_comms.Id] = FreezeMode.Immediately;
        var resources = new ResourceGovernor(sessions, table, governor, new FakeSystemMemory(), new FakeAudioActivity(),
            new FakeThrottleLedger(), settings, _clock, ownProcessId: 999);
        resources.Tick();
        _clock.Now += TimeSpan.FromSeconds(6);
        resources.Tick();
        Assert.Contains(20, governor.Suspended);

        var close = new CategoryCloseService(_library, _switcher, _windowManager, _temporary, resources, new FakeDelay());
        // Window 20 belongs to process 20, the frozen one.
        OpenIn(_comms, 20);
        _switcher.SwitchTo(_work.Id);

        close.Close(_comms.Id);

        Assert.DoesNotContain(20, governor.Suspended);
    }
}
