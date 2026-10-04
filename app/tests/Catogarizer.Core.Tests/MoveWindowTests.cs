using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

/// <summary>Moving one window to another category without switching (service and library-aware switcher).</summary>
public sealed class MoveWindowTests
{
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly FakeWindowWatcher _windowWatcher = new();
    private readonly FakeCategoryActionService _categoryActions = new();
    private readonly FakeHiddenWindowStore _hiddenStore = new();
    private readonly LibraryService _library;
    private readonly CategorySwitchService _service;
    private readonly CategorySwitcher _switcher;

    public MoveWindowTests()
    {
        _library = new LibraryService(new FakeConfigStore(), fileExists: _ => true);
        _service = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, _categoryActions,
            () => _library.PinnedApps, new FakeClock(), _hiddenStore, ownProcessId: 4242);
        _switcher = new CategorySwitcher(_library, _service, new FakeAppBlockingService());
    }

    private static OpenWindowInfo Window(int handle, string processName) => new(new IntPtr(handle), "Window", processName, handle);

    private IReadOnlyList<OpenWindowInfo> Session(Guid id) =>
        _service.GetSessions().TryGetValue(id, out var s) ? s : [];

    [Fact]
    public void MoveWindow_ToAnInactiveCategory_ParksTheWindowAndLedgersItBeforeHiding()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var comms = Guid.NewGuid();
        List<HiddenWindowRecord> ledgerAtHide = [];
        _windowManager.OnHide = _ => ledgerAtHide = _hiddenStore.Records.ToList();

        var moved = _service.MoveWindow(new IntPtr(1), comms);

        Assert.True(moved);
        Assert.Contains(new IntPtr(1), _windowManager.HideCalls);
        Assert.Single(ledgerAtHide);
        Assert.Single(_hiddenStore.Records);
        Assert.Empty(Session(CategorySwitchService.Uncategorized));
        Assert.Equal(new IntPtr(1), Session(comms).Single().Handle);
        Assert.Equal(CategorySwitchService.Uncategorized, _service.ActiveCategoryId);
    }

    [Fact]
    public void MoveWindow_ToTheActiveCategory_ShowsAParkedWindowAndClearsTheLedger()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var comms = Guid.NewGuid();
        _service.MoveWindow(new IntPtr(1), comms);
        _service.SwitchTo(comms, []); // comms is active, window is back
        _service.MoveWindow(new IntPtr(1), CategorySwitchService.Uncategorized); // parked in Unsorted now
        Assert.Single(_hiddenStore.Records);

        var moved = _service.MoveWindow(new IntPtr(1), comms);

        Assert.True(moved);
        Assert.True(_windowManager.IsWindowVisible(new IntPtr(1)));
        Assert.Empty(_hiddenStore.Records);
        Assert.Equal(new IntPtr(1), Session(comms).Single().Handle);
        Assert.Empty(Session(CategorySwitchService.Uncategorized));
    }

    [Fact]
    public void MoveWindow_BetweenTwoInactiveCategories_KeepsItHiddenAndLedgered()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _service.MoveWindow(new IntPtr(1), a);

        _service.MoveWindow(new IntPtr(1), b);

        Assert.False(_windowManager.IsWindowVisible(new IntPtr(1)));
        Assert.Single(_hiddenStore.Records);
        Assert.Empty(Session(a));
        Assert.Single(Session(b));
    }

    [Fact]
    public void MoveWindow_ToItsOwnCategory_IsASuccessfulNoOp()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();

        var moved = _service.MoveWindow(new IntPtr(1), CategorySwitchService.Uncategorized);

        Assert.True(moved);
        Assert.Empty(_windowManager.HideCalls);
        Assert.Empty(_windowManager.ShowCalls);
    }

    [Fact]
    public void MoveWindow_UnknownOrClosedWindow_ReturnsFalseAndChangesNothing()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var comms = Guid.NewGuid();
        _windowManager.SimulateClosedExternally(new IntPtr(1));

        Assert.False(_service.MoveWindow(new IntPtr(1), comms));
        Assert.False(_service.MoveWindow(new IntPtr(99), comms));
        Assert.Empty(_windowManager.HideCalls);
        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void MoveWindow_SendsStateChanged()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var raised = 0;
        _service.StateChanged += () => raised++;

        _service.MoveWindow(new IntPtr(1), Guid.NewGuid());

        Assert.Equal(1, raised);
    }

    [Fact]
    public void MovedWindow_ComesBackWithItsCategoryOnSwitch()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var comms = Guid.NewGuid();
        _service.MoveWindow(new IntPtr(1), comms);

        _service.SwitchTo(comms, []);

        Assert.True(_windowManager.IsWindowVisible(new IntPtr(1)));
        Assert.Empty(_hiddenStore.Records);
    }

    // ---- CategorySwitcher: only existing categories (or Unsorted) are valid targets ----

    [Fact]
    public void Switcher_MoveWindow_ToAnExistingCategory_Works()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var comms = _library.AddCategory("Comms");

        Assert.True(_switcher.MoveWindow(new IntPtr(1), comms.Id));

        Assert.Equal(new IntPtr(1), Session(comms.Id).Single().Handle);
    }

    [Fact]
    public void Switcher_MoveWindow_ToADeletedCategory_IsRefused()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();

        Assert.False(_switcher.MoveWindow(new IntPtr(1), Guid.NewGuid()));

        Assert.Empty(_windowManager.HideCalls);
    }

    [Fact]
    public void Switcher_MoveWindow_ToUnsorted_IsAlwaysAllowed()
    {
        _windowFinder.RunningWindows.Add(Window(1, "notepad"));
        _service.Initialize();
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);
        _service.MoveWindow(new IntPtr(1), comms.Id);
        Assert.True(_windowManager.IsWindowVisible(new IntPtr(1)));

        Assert.True(_switcher.MoveWindow(new IntPtr(1), CategorySwitchService.Uncategorized));

        Assert.False(_windowManager.IsWindowVisible(new IntPtr(1)));
    }
}
