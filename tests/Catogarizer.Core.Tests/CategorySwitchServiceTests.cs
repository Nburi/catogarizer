using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class CategorySwitchServiceTests
{
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly FakeWindowWatcher _windowWatcher = new();
    private readonly FakeCategoryActionService _categoryActionService = new();
    private readonly List<PinnedApp> _pinnedApps = [];
    private readonly CategorySwitchService _service;

    public CategorySwitchServiceTests()
    {
        _service = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, _categoryActionService, () => _pinnedApps);
    }

    private static OpenWindowInfo Window(int handle, string processName, string title = "Window") =>
        new(new IntPtr(handle), title, processName, handle);

    private static AppEntry App(string name) => new() { Name = name, ExecutablePath = $@"C:\{name}.exe" };

    [Fact]
    public void Initialize_SnapshotsCurrentWindowsIntoUncategorizedAndStartsWatcher()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer"));
        _windowFinder.RunningWindows.Add(Window(2, "outlook"));

        _service.Initialize();

        Assert.Equal(CategorySwitchService.Uncategorized, _service.ActiveCategoryId);
        Assert.True(_windowWatcher.IsStarted);
    }

    [Fact]
    public void Initialize_ExcludesPinnedAppsFromTheSnapshot()
    {
        _windowFinder.RunningWindows.Add(Window(1, "spotify"));
        _windowFinder.RunningWindows.Add(Window(2, "explorer"));
        _pinnedApps.Add(new PinnedApp { Name = "Spotify", ProcessNameOrPath = "spotify" });

        _service.Initialize();
        // Switching away should only hide the non-pinned window - proves spotify was never tracked.
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, [App("Learning")]);

        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
        Assert.Contains(new IntPtr(2), _windowManager.HideCalls);
    }

    [Fact]
    public void SwitchTo_SameCategoryAlreadyActive_IsNoOp()
    {
        _service.Initialize();

        _service.SwitchTo(CategorySwitchService.Uncategorized, []);

        Assert.Empty(_windowManager.HideCalls);
        Assert.Empty(_categoryActionService.Calls);
    }

    [Fact]
    public void SwitchTo_NoSavedSession_OpensTheTemplate()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        var template = new List<AppEntry> { App("Spotify"), App("ExamList") };

        _service.SwitchTo(learning, template);

        Assert.Equal(learning, _service.ActiveCategoryId);
        Assert.Contains("Open:Spotify,ExamList", _categoryActionService.Calls);
    }

    [Fact]
    public void SwitchTo_HidesTheOutgoingCategorysTrackedWindows()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer"));
        _service.Initialize();
        var learning = Guid.NewGuid();

        _service.SwitchTo(learning, [App("Spotify")]);

        Assert.Contains(new IntPtr(1), _windowManager.HideCalls);
    }

    [Fact]
    public void SwitchTo_SkipsHidingWindowsAlreadyClosedExternally()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer"));
        _service.Initialize();
        _windowManager.SimulateClosedExternally(new IntPtr(1));

        _service.SwitchTo(Guid.NewGuid(), [App("Spotify")]);

        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
    }

    [Fact]
    public void WindowAppeared_WhileACategoryIsActive_GetsAttributedToItsSession()
    {
        _service.Initialize();
        var programming = Guid.NewGuid();
        _service.SwitchTo(programming, []); // no template apps - session starts empty

        _windowWatcher.RaiseWindowAppeared(Window(5, "Code", "VS Code"));

        // Switching away should now hide the ad-hoc VS Code window.
        _service.SwitchTo(Guid.NewGuid(), []);
        Assert.Contains(new IntPtr(5), _windowManager.HideCalls);
    }

    [Fact]
    public void WindowAppeared_ForAPinnedApp_IsIgnored()
    {
        _pinnedApps.Add(new PinnedApp { Name = "WhatsApp", ProcessNameOrPath = "WhatsApp.exe" });
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);

        _windowWatcher.RaiseWindowAppeared(Window(9, "WhatsApp"));
        _service.SwitchTo(Guid.NewGuid(), []);

        Assert.DoesNotContain(new IntPtr(9), _windowManager.HideCalls);
    }

    [Fact]
    public void SwitchTo_RestoresAPreviouslySavedSessionInsteadOfReopeningTheTemplate()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        var programming = Guid.NewGuid();

        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD", "Thesis.docx"));
        _service.SwitchTo(programming, [App("VSCode")]);
        _categoryActionService.Calls.Clear();

        _service.SwitchTo(learning, [App("Spotify")]); // template passed again but must NOT be reopened

        Assert.Contains(new IntPtr(7), _windowManager.ShowCalls);
        Assert.DoesNotContain("Open:Spotify", _categoryActionService.Calls);
    }

    [Fact]
    public void SwitchTo_PrunesWindowsClosedWhileTheirCategoryWasInactive()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD"));
        _service.SwitchTo(Guid.NewGuid(), []);

        _windowManager.SimulateClosedExternally(new IntPtr(7)); // user closed Word while Learning was hidden

        // No saved windows left open -> falls back to (re)opening the template.
        _service.SwitchTo(learning, [App("Spotify")]);

        Assert.DoesNotContain(new IntPtr(7), _windowManager.ShowCalls);
        Assert.Contains("Open:Spotify", _categoryActionService.Calls);
    }
}
