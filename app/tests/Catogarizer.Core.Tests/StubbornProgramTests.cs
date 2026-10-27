using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

/// <summary>Ending a program that did not close politely: what is shown first, what is never touched, what the result says.</summary>
public sealed class StubbornProgramTests
{
    private const int OwnPid = 4242;

    private readonly FakeConfigStore _store = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly FakeWindowWatcher _windowWatcher = new();
    private readonly FakeClock _clock = new();
    private readonly LibraryService _library;
    private readonly CategorySwitchService _switchService;
    private readonly CategorySwitcher _switcher;
    private readonly StubbornProgramService _programs;
    private readonly Category _work;
    private readonly Category _comms;

    public StubbornProgramTests()
    {
        _library = new LibraryService(_store, fileExists: _ => true);
        _switchService = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, new FakeCategoryActionService(),
            () => _library.PinnedApps, _clock, new FakeHiddenWindowStore(), ownProcessId: OwnPid);
        _switcher = new CategorySwitcher(_library, _switchService, new FakeAppBlockingService());
        _programs = new StubbornProgramService(_library, _switcher, _windowManager, OwnPid);
        _work = _library.AddCategory("Work");
        _comms = _library.AddCategory("Comms");
        _switchService.Initialize();
    }

    /// <summary>A window of process <paramref name="pid"/> that appears while <paramref name="category"/> is active.</summary>
    private OpenWindowInfo OpenIn(Category category, int handle, int pid, string process = "app")
    {
        if (_switcher.ActiveCategoryId != category.Id) _switcher.SwitchTo(category.Id);
        var window = new OpenWindowInfo(new IntPtr(handle), $"Window {handle}", process, pid);
        _windowManager.ProcessIds[window.Handle] = pid;
        _windowWatcher.RaiseWindowAppeared(window);
        return window;
    }

    [Fact]
    public void Describe_GroupsWindowsByProgram()
    {
        var a = OpenIn(_work, 1, pid: 10);
        var b = OpenIn(_work, 2, pid: 10);
        var c = OpenIn(_work, 3, pid: 20);

        var programs = _programs.Describe([a, b, c]);

        Assert.Equal(2, programs.Count);
        Assert.Equal([a, b], programs.Single(p => p.ProcessId == 10).Windows);
        Assert.Equal([c], programs.Single(p => p.ProcessId == 20).Windows);
    }

    [Fact]
    public void Describe_ADialogOfItsOwn_MeansItAsksYou()
    {
        var window = OpenIn(_work, 1, pid: 10);
        _windowManager.WithDialog.Add(window.Handle);

        Assert.Equal(StayReason.AsksYou, _programs.Describe([window]).Single().Reason);
    }

    [Fact]
    public void Describe_AVisibleWindowWithoutDialog_IsStillOpen()
    {
        var window = OpenIn(_work, 1, pid: 10);

        Assert.Equal(StayReason.StillOpen, _programs.Describe([window]).Single().Reason);
    }

    [Fact]
    public void Describe_AWindowOutOfSight_KeepsRunningInTheBackground()
    {
        var window = OpenIn(_comms, 1, pid: 10);
        _switcher.SwitchTo(_work.Id); // parks Comms: its windows are hidden

        Assert.Equal(StayReason.Background, _programs.Describe([window]).Single().Reason);
    }

    [Fact]
    public void Describe_ADialogWinsOverVisibility()
    {
        var shown = OpenIn(_work, 1, pid: 10);
        var other = OpenIn(_work, 2, pid: 10);
        _windowManager.WithDialog.Add(other.Handle);

        Assert.Equal(StayReason.AsksYou, _programs.Describe([shown, other]).Single().Reason);
    }

    [Fact]
    public void Describe_LeavesOutWindowsThatClosedMeanwhile()
    {
        var gone = OpenIn(_work, 1, pid: 10);
        var stays = OpenIn(_work, 2, pid: 20);
        _windowManager.SimulateClosedExternally(gone.Handle);

        var programs = _programs.Describe([gone, stays]);

        Assert.Equal(20, Assert.Single(programs).ProcessId);
    }

    [Fact]
    public void Describe_NamesTheWindowsEndingTheProgramTakesAlongInOtherCategories()
    {
        var stays = OpenIn(_comms, 1, pid: 10);
        OpenIn(_work, 2, pid: 10);
        OpenIn(_work, 3, pid: 10);
        OpenIn(_work, 4, pid: 20); // another program: not affected

        var shared = _programs.Describe([stays]).Single().Shared;

        var entry = Assert.Single(shared);
        Assert.Equal("Work", entry.CategoryName);
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public void Describe_NothingSharedWhenTheProgramLivesOnlyHere()
    {
        var stays = OpenIn(_comms, 1, pid: 10);
        OpenIn(_work, 2, pid: 20);

        Assert.Empty(_programs.Describe([stays]).Single().Shared);
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("Explorer")]
    [InlineData("explorer.exe")]
    [InlineData("dwm")]
    [InlineData("winlogon")]
    [InlineData("ApplicationFrameHost")]
    public void Describe_TheShellAndSystemProcesses_AreProtected(string process)
    {
        var window = OpenIn(_work, 1, pid: 10, process);

        Assert.True(_programs.Describe([window]).Single().IsProtected);
    }

    [Fact]
    public void Describe_CatogarizerItself_IsProtected()
    {
        var window = OpenIn(_work, 1, pid: OwnPid, "Catogarizer.App");

        Assert.True(_programs.Describe([window]).Single().IsProtected);
    }

    [Fact]
    public void Describe_AnOrdinaryProgram_IsNotProtected()
    {
        var window = OpenIn(_work, 1, pid: 10, "notepad");

        Assert.False(_programs.Describe([window]).Single().IsProtected);
    }

    [Fact]
    public void End_EndsTheProcess_AndAllItsWindowsGoWithIt()
    {
        var a = OpenIn(_work, 1, pid: 10);
        var b = OpenIn(_work, 2, pid: 10);
        var program = _programs.Describe([a, b]).Single();

        var result = _programs.End(program);

        Assert.Equal(ProgramEndOutcome.Ended, result.Outcome);
        Assert.True(result.IsDone);
        Assert.Single(_windowManager.ForceKillCalls); // one process, one kill
        Assert.False(_windowManager.IsWindowOpen(a.Handle));
        Assert.False(_windowManager.IsWindowOpen(b.Handle));
    }

    [Fact]
    public void End_AProtectedProgram_IsNeverKilled()
    {
        var window = OpenIn(_work, 1, pid: 10, "explorer");
        var program = _programs.Describe([window]).Single();

        var result = _programs.End(program);

        Assert.Equal(ProgramEndOutcome.Protected, result.Outcome);
        Assert.False(result.IsDone);
        Assert.Empty(_windowManager.ForceKillCalls);
        Assert.True(_windowManager.IsWindowOpen(window.Handle));
    }

    [Fact]
    public void End_WithoutTheRights_ReportsItAndLeavesTheWindow()
    {
        var window = OpenIn(_work, 1, pid: 10);
        var program = _programs.Describe([window]).Single();
        _windowManager.ForceKillResult = ForceKillOutcome.AccessDenied;

        var result = _programs.End(program);

        Assert.Equal(ProgramEndOutcome.AccessDenied, result.Outcome);
        Assert.False(result.IsDone);
        Assert.True(_windowManager.IsWindowOpen(window.Handle));
    }

    [Fact]
    public void End_WhenTheWindowIsGoneBeforeTheClick_KillsNothing()
    {
        var window = OpenIn(_work, 1, pid: 10);
        var program = _programs.Describe([window]).Single();
        _windowManager.SimulateClosedExternally(window.Handle);

        var result = _programs.End(program);

        Assert.Equal(ProgramEndOutcome.AlreadyGone, result.Outcome);
        Assert.True(result.IsDone);
        Assert.Empty(_windowManager.ForceKillCalls);
    }

    [Fact]
    public void End_WhenWindowsReusedTheHandleForAnotherProgram_KillsNothing()
    {
        var window = OpenIn(_work, 1, pid: 10);
        var program = _programs.Describe([window]).Single();
        _windowManager.ProcessIds[window.Handle] = 99; // the handle now belongs to somebody else

        var result = _programs.End(program);

        Assert.Equal(ProgramEndOutcome.AlreadyGone, result.Outcome);
        Assert.Empty(_windowManager.ForceKillCalls);
    }

    [Fact]
    public void End_ARealFailure_IsReportedAsFailed()
    {
        var window = OpenIn(_work, 1, pid: 10);
        var program = _programs.Describe([window]).Single();
        _windowManager.ForceKillResult = ForceKillOutcome.Failed;

        Assert.Equal(ProgramEndOutcome.Failed, _programs.End(program).Outcome);
    }
}
