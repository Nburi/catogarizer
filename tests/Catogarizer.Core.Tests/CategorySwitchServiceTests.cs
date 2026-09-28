using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
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
    private readonly FakeClock _clock = new();
    private readonly FakeHiddenWindowStore _hiddenStore = new();
    private const int OwnProcessId = 4242;
    private readonly CategorySwitchService _service;

    public CategorySwitchServiceTests()
    {
        _service = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, _categoryActionService,
            () => _pinnedApps, _clock, _hiddenStore, OwnProcessId);
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

    // ---- own windows ----

    [Fact]
    public void Initialize_NeverTracksCatogarizersOwnWindows()
    {
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(1), "Catogarizer", "Catogarizer.App", OwnProcessId));
        _windowFinder.RunningWindows.Add(Window(2, "explorer"));
        _service.Initialize();

        _service.SwitchTo(Guid.NewGuid(), []);

        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
        Assert.Contains(new IntPtr(2), _windowManager.HideCalls);
    }

    [Fact]
    public void WindowAppeared_FromCatogarizerItself_IsIgnored()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);

        _windowWatcher.RaiseWindowAppeared(new OpenWindowInfo(new IntPtr(3), "Palette", "Catogarizer.App", OwnProcessId));
        _service.SwitchTo(Guid.NewGuid(), []);

        Assert.DoesNotContain(new IntPtr(3), _windowManager.HideCalls);
    }

    // ---- safety net ----

    [Fact]
    public void SwitchTo_RecordsHiddenWindowsInTheStore()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer", "Downloads"));
        _service.Initialize();

        _service.SwitchTo(Guid.NewGuid(), []);

        var record = Assert.Single(_hiddenStore.Records);
        Assert.Equal(1, record.Handle);
        Assert.Equal(1, record.ProcessId);
    }

    [Fact]
    public void SwitchTo_RestoringASession_RemovesItsWindowsFromTheStore()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer"));
        _service.Initialize();
        _service.SwitchTo(Guid.NewGuid(), []);

        _service.SwitchTo(CategorySwitchService.Uncategorized, []);

        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void Initialize_ShowsWindowsAPreviousRunLeftHidden()
    {
        _hiddenStore.Records.Add(new HiddenWindowRecord(3, 3, "WINWORD", "Thesis.docx"));
        _windowManager.SimulateHidden(new IntPtr(3));

        _service.Initialize();

        Assert.Contains(new IntPtr(3), _windowManager.ShowCalls);
        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void Initialize_DoesNotShowARecordedHandleNowOwnedByAnotherProcess()
    {
        _hiddenStore.Records.Add(new HiddenWindowRecord(3, 3, "WINWORD", "Thesis.docx"));
        _windowManager.SimulateHidden(new IntPtr(3));
        _windowManager.ProcessIds[new IntPtr(3)] = 999; // handle reused after a reboot

        _service.Initialize();

        Assert.DoesNotContain(new IntPtr(3), _windowManager.ShowCalls);
    }

    [Fact]
    public void ShowAllAndReset_ShowsEveryHiddenWindowAndMakesUnsortedActive()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer"));
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD"));
        _service.SwitchTo(Guid.NewGuid(), []);

        _service.ShowAllAndReset();

        Assert.Contains(new IntPtr(1), _windowManager.ShowCalls);
        Assert.Contains(new IntPtr(7), _windowManager.ShowCalls);
        Assert.Equal(CategorySwitchService.Uncategorized, _service.ActiveCategoryId);
        Assert.Empty(_hiddenStore.Records);
        var unsorted = _service.GetSessions()[CategorySwitchService.Uncategorized];
        Assert.Equal(2, unsorted.Count);
    }

    // ---- state for the UI ----

    [Fact]
    public void SwitchTo_TracksPreviousCategoryAndActiveSince()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        _clock.Now = new DateTime(2026, 9, 28, 9, 12, 0);

        _service.SwitchTo(learning, []);

        Assert.Equal(CategorySwitchService.Uncategorized, _service.PreviousCategoryId);
        Assert.Equal(new DateTime(2026, 9, 28, 9, 12, 0), _service.ActiveSince);
    }

    [Fact]
    public void SwitchTo_RaisesStateChanged()
    {
        _service.Initialize();
        var raised = 0;
        _service.StateChanged += () => raised++;

        _service.SwitchTo(Guid.NewGuid(), []);

        Assert.True(raised >= 1);
    }

    [Fact]
    public void SwitchTo_ATemplateLaunch_AnnouncesTheNewCategoryBeforeAppsOpen()
    {
        _service.Initialize();
        var research = Guid.NewGuid();
        var seenBeforeLaunch = new List<(Guid Active, bool Opening)>();
        _service.StateChanged += () => seenBeforeLaunch.Add((_service.ActiveCategoryId, _service.IsOpeningApps));
        _categoryActionService.OnOpen = () => seenBeforeLaunch.Add((Guid.Empty, true)); // marks the launch moment

        _service.SwitchTo(research, [App("Zotero")]);

        Assert.Equal((research, true), seenBeforeLaunch[0]);
        Assert.Equal((research, false), seenBeforeLaunch[^1]);
        Assert.False(_service.IsOpeningApps);
    }

    [Fact]
    public void SwitchTo_ARestore_NeverReportsOpeningApps()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD"));
        _service.SwitchTo(Guid.NewGuid(), []);
        var sawOpening = false;
        _service.StateChanged += () => sawOpening |= _service.IsOpeningApps;

        _service.SwitchTo(learning, [App("Word")]);

        Assert.False(sawOpening);
    }

    [Fact]
    public void GetSessions_ReportsOpenWindowsWithTheirCurrentTitles()
    {
        _windowFinder.RunningWindows.Add(Window(1, "Code", "old title"));
        _windowManager.Titles[new IntPtr(1)] = "CategorySwitchService.cs - catogarizer";
        _service.Initialize();

        var window = Assert.Single(_service.GetSessions()[CategorySwitchService.Uncategorized]);

        Assert.Equal("CategorySwitchService.cs - catogarizer", window.Title);
    }

    [Fact]
    public void SwitchTo_ReportsWhetherTheSessionWasRestoredAndLaunchFailures()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        _categoryActionService.FailingApps.Add("Zotero");

        var result = _service.SwitchTo(learning, [App("Obsidian"), App("Zotero")]);

        Assert.False(result.RestoredSession);
        Assert.Equal(1, result.WindowCount);
        Assert.Equal("Zotero", Assert.Single(result.Failures).App.Name);
    }

    // ---- template apps already open ----

    [Fact]
    public void SwitchTo_TemplateAppAlreadyOpenInUnsorted_IsTakenOverInsteadOfHidden()
    {
        _windowFinder.RunningWindows.Add(Window(1, "charmap", "Zeichentabelle"));
        _windowFinder.RunningWindows.Add(Window(2, "explorer"));
        _service.Initialize();
        var deepWork = Guid.NewGuid();

        _service.SwitchTo(deepWork, [App("charmap")]);

        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
        Assert.Contains(new IntPtr(2), _windowManager.HideCalls);
        var sessions = _service.GetSessions();
        Assert.Contains(sessions[deepWork], w => w.Handle == new IntPtr(1));
        Assert.DoesNotContain(sessions[CategorySwitchService.Uncategorized], w => w.Handle == new IntPtr(1));
    }

    [Fact]
    public void SwitchTo_TemplateAppHiddenInAParkedUnsorted_IsShownAndTakenOver()
    {
        _windowFinder.RunningWindows.Add(Window(1, "charmap"));
        _service.Initialize();
        _service.SwitchTo(Guid.NewGuid(), []); // Unsorted parked, charmap hidden
        var deepWork = Guid.NewGuid();

        _service.SwitchTo(deepWork, [App("charmap")]);

        Assert.Contains(new IntPtr(1), _windowManager.ShowCalls);
        Assert.Contains(_service.GetSessions()[deepWork], w => w.Handle == new IntPtr(1));
        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void SwitchTo_NeverTakesATemplateAppsWindowFromAnotherRealCategory()
    {
        _service.Initialize();
        var comms = Guid.NewGuid();
        _service.SwitchTo(comms, []);
        _windowWatcher.RaiseWindowAppeared(Window(8, "chrome", "Gmail"));
        var research = Guid.NewGuid();

        _service.SwitchTo(research, [App("chrome")]);

        Assert.Contains(new IntPtr(8), _windowManager.HideCalls);
        Assert.Contains(_service.GetSessions()[comms], w => w.Handle == new IntPtr(8));
    }

    [Fact]
    public void SwitchTo_TakesOnlyOneWindowPerTemplateApp_PreferringATitleMatch()
    {
        _windowFinder.RunningWindows.Add(Window(1, "msedge", "Bing - Microsoft Edge"));
        _windowFinder.RunningWindows.Add(Window(2, "msedge", "nothing-to-do"));
        _service.Initialize();
        var focus = Guid.NewGuid();
        var pwa = new AppEntry { Name = "nothing-to-do", ExecutablePath = @"C:\Edge\msedge_proxy.exe" };

        _service.SwitchTo(focus, [pwa]);

        var taken = Assert.Single(_service.GetSessions()[focus]);
        Assert.Equal(new IntPtr(2), taken.Handle);
        Assert.Contains(new IntPtr(1), _windowManager.HideCalls);
    }

    [Fact]
    public void SwitchTo_ACategoryWithASavedSession_DoesNotTakeUnsortedWindows()
    {
        _service.Initialize();
        var deepWork = Guid.NewGuid();
        _service.SwitchTo(deepWork, []);
        _windowWatcher.RaiseWindowAppeared(Window(5, "Code"));
        _service.SwitchTo(CategorySwitchService.Uncategorized, []);
        _windowWatcher.RaiseWindowAppeared(Window(6, "charmap"));

        _service.SwitchTo(deepWork, [App("charmap")]);

        Assert.Contains(new IntPtr(6), _windowManager.HideCalls);
    }

    // ---- pinning while running ----

    [Fact]
    public void ApplyPinnedApps_ShowsAParkedWindowOfANewlyPinnedApp_AndNeverHidesItAgain()
    {
        _windowFinder.RunningWindows.Add(Window(1, "Spotify"));
        _service.Initialize();
        var deepWork = Guid.NewGuid();
        _service.SwitchTo(deepWork, []); // Spotify hidden with Unsorted

        _pinnedApps.Add(new PinnedApp { Name = "Spotify", ProcessNameOrPath = "Spotify.exe" });
        _service.ApplyPinnedApps();
        _service.SwitchTo(CategorySwitchService.Uncategorized, []);
        _windowManager.HideCalls.Clear();
        _service.SwitchTo(deepWork, []);

        Assert.Contains(new IntPtr(1), _windowManager.ShowCalls);
        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void SwitchTo_NeverHidesAWindowWhoseAppWasPinnedAfterItWasTracked()
    {
        _windowFinder.RunningWindows.Add(Window(1, "WhatsApp"));
        _service.Initialize();
        _pinnedApps.Add(new PinnedApp { Name = "WhatsApp", ProcessNameOrPath = "WhatsApp" });

        _service.SwitchTo(Guid.NewGuid(), []);

        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
    }

    // ---- deleted categories ----

    [Fact]
    public void ReleaseCategory_ParkedWhileUnsortedIsActive_ShowsItsWindowsInUnsorted()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD"));
        _service.SwitchTo(CategorySwitchService.Uncategorized, []);

        _service.ReleaseCategory(learning);

        Assert.Contains(new IntPtr(7), _windowManager.ShowCalls);
        Assert.Contains(_service.GetSessions()[CategorySwitchService.Uncategorized], w => w.Handle == new IntPtr(7));
        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void ReleaseCategory_Active_MakesUnsortedActiveAndBringsBackItsParkedWindows()
    {
        _windowFinder.RunningWindows.Add(Window(1, "explorer"));
        _service.Initialize();
        var learning = Guid.NewGuid();
        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD"));

        _service.ReleaseCategory(learning);

        Assert.Equal(CategorySwitchService.Uncategorized, _service.ActiveCategoryId);
        Assert.Contains(new IntPtr(1), _windowManager.ShowCalls);
        Assert.DoesNotContain(new IntPtr(7), _windowManager.HideCalls);
        Assert.Null(_service.PreviousCategoryId);
        Assert.Empty(_hiddenStore.Records);
    }

    [Fact]
    public void ReleaseCategory_ParkedWhileAnotherIsActive_KeepsItsWindowsHiddenButReachableViaUnsorted()
    {
        _service.Initialize();
        var learning = Guid.NewGuid();
        var programming = Guid.NewGuid();
        _service.SwitchTo(learning, []);
        _windowWatcher.RaiseWindowAppeared(Window(7, "WINWORD"));
        _service.SwitchTo(programming, []);

        _service.ReleaseCategory(learning);
        _service.SwitchTo(CategorySwitchService.Uncategorized, []);

        Assert.Contains(new IntPtr(7), _windowManager.ShowCalls);
    }

    // ---- attribution ----

    [Fact]
    public void SwitchTo_AttributesTemplateLaunchedWindowsImmediately()
    {
        _service.Initialize();
        var programming = Guid.NewGuid();
        _categoryActionService.LaunchHandles["VSCode"] = new IntPtr(20);

        _service.SwitchTo(programming, [App("VSCode")]);
        // Switch away before the watcher's next tick would have seen the new window.
        _service.SwitchTo(Guid.NewGuid(), []);

        Assert.Contains(new IntPtr(20), _windowManager.HideCalls);
    }

    [Fact]
    public void WindowAppeared_ForAWindowOfAParkedSession_MovesItToTheActiveCategory()
    {
        _service.Initialize();
        var comms = Guid.NewGuid();
        var deepWork = Guid.NewGuid();
        _service.SwitchTo(comms, []);
        _windowWatcher.RaiseWindowAppeared(Window(8, "Teams"));
        _service.SwitchTo(deepWork, []);

        // Teams shows itself on a new message while Comms is parked.
        _windowWatcher.RaiseWindowAppeared(Window(8, "Teams"));

        var sessions = _service.GetSessions();
        Assert.Contains(sessions[deepWork], w => w.Handle == new IntPtr(8));
        Assert.DoesNotContain(sessions[comms], w => w.Handle == new IntPtr(8));
        Assert.Empty(_hiddenStore.Records);
    }
}
