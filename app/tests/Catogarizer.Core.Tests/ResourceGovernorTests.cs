using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Resources;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class ResourceGovernorTests
{
    private const int OwnPid = 999;

    private static readonly Guid Work = Guid.NewGuid();
    private static readonly Guid Comms = Guid.NewGuid();
    private static readonly Guid Design = Guid.NewGuid();

    private readonly FakeWindowSessions _sessions = new() { ActiveCategoryId = Work };
    private readonly FakeProcessTable _table = new();
    private readonly FakeProcessGovernor _governor = new();
    private readonly FakeSystemMemory _memory = new();
    private readonly FakeAudioActivity _audio = new();
    private readonly FakeThrottleLedger _ledger = new();
    private readonly FakeResourceSettings _settings = new();
    private readonly FakeClock _clock = new();
    private readonly ResourceGovernor _resources;

    public ResourceGovernorTests()
    {
        _resources = new ResourceGovernor(_sessions, _table, _governor, _memory, _audio, _ledger, _settings, _clock, OwnPid);
    }

    /// <summary>The usual picture: Work is active with one app, Comms is parked with one app.</summary>
    private void WorkActiveCommsParked()
    {
        _table.Add(10, 1, "code").Add(20, 1, "slack");
        _sessions.Add(Work, 10, "code");
        _sessions.Add(Comms, 20, "slack");
    }

    private void Later(TimeSpan span) => _clock.Now += span;

    // ---------------- Eco ----------------

    [Fact]
    public void Tick_ParkedProcessGoesToEco_ActiveStaysNormal()
    {
        WorkActiveCommsParked();

        _resources.Tick();

        Assert.Equal([20], _governor.Eco);
        Assert.Contains(_ledger.Records, r => r.ProcessId == 20 && r.Level == ResourceLevel.Eco && r.Memento == FakeProcessGovernor.Original);
        Assert.DoesNotContain(_ledger.Records, r => r.ProcessId == 10);
    }

    [Fact]
    public void Tick_TheWholeProcessTreeIsIncluded()
    {
        WorkActiveCommsParked();
        _table.Add(21, 20, "slack").Add(22, 21, "slack").Add(30, 1, "other");

        _resources.Tick();

        Assert.Equal([20, 21, 22], _governor.Eco.Order());
    }

    [Fact]
    public void Tick_AChildOlderThanItsParent_IsAReusedIdAndLeftAlone()
    {
        WorkActiveCommsParked();
        // Pid 25 names 20 as its parent but started long before it: the real parent exited and 20 was reused.
        _table.Add(25, 20, "stranger", startTicks: 5);

        _resources.Tick();

        Assert.Equal([20], _governor.Eco);
    }

    [Fact]
    public void Tick_WhenTheCategoryBecomesActive_ProcessesAreRestoredAndTheLedgerEmptied()
    {
        WorkActiveCommsParked();
        _resources.Tick();

        _sessions.ActiveCategoryId = Comms;
        _resources.Tick();

        // Comms is shown again, so its process is back to normal; Work is the one parked now.
        Assert.DoesNotContain(20, _governor.Eco);
        Assert.Equal([(20, FakeProcessGovernor.Original)], _governor.LeaveEcoCalls);
        Assert.DoesNotContain(_ledger.Records, r => r.ProcessId == 20);
        Assert.Contains(10, _governor.Eco);
    }

    [Fact]
    public void Tick_AProcessWithAWindowInTheActiveCategory_IsLeftAloneWithItsWholeTree()
    {
        _table.Add(20, 1, "chrome").Add(21, 20, "chrome");
        _sessions.Add(Work, 20, "chrome");
        _sessions.Add(Comms, 20, "chrome");

        _resources.Tick();

        Assert.Empty(_governor.EnterEcoCalls);
    }

    [Fact]
    public void Tick_AChildOwningAnActiveWindow_ProtectsTheParentTree()
    {
        _table.Add(20, 1, "app").Add(21, 20, "app");
        _sessions.Add(Comms, 20, "app");
        _sessions.Add(Work, 21, "app");

        _resources.Tick();

        Assert.Empty(_governor.EnterEcoCalls);
    }

    [Fact]
    public void Tick_PinnedSystemAndOwnProcesses_AreLeftAlone()
    {
        _table.Add(20, 1, "spotify").Add(30, 1, "explorer").Add(40, 1, "ApplicationFrameHost").Add(OwnPid, 1, "Catogarizer.App");
        _settings.Pinned.Add("spotify");
        _sessions.Add(Comms, 20, "spotify");
        _sessions.Add(Comms, 30, "explorer");
        _sessions.Add(Comms, 40, "ApplicationFrameHost");
        _sessions.Add(Comms, OwnPid, "Catogarizer.App");

        _resources.Tick();

        Assert.Empty(_governor.EnterEcoCalls);
    }

    [Fact]
    public void Tick_ATreePlayingSound_IsLeftAloneAtEveryLevel()
    {
        WorkActiveCommsParked();
        _table.Add(21, 20, "slack");
        _audio.Playing.Add(21);
        _memory.LoadPercent = 95;
        Later(TimeSpan.FromMinutes(5));

        _resources.Tick();
        Later(TimeSpan.FromMinutes(5));
        _resources.Tick();

        Assert.Empty(_governor.EnterEcoCalls);
        Assert.Empty(_governor.TrimCalls);
        Assert.Empty(_governor.SuspendCalls);
    }

    [Fact]
    public void Tick_WithTheSettingOff_TouchesNothing_AndReleasesWhatWasApplied()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Assert.NotEmpty(_governor.Eco);

        _settings.Enabled = false;
        _resources.Tick();

        Assert.Empty(_governor.Eco);
        Assert.Empty(_ledger.Records);
        _governor.EnterEcoCalls.Clear();
        _resources.Tick();
        Assert.Empty(_governor.EnterEcoCalls);
    }

    [Fact]
    public void Tick_AProcessThatCannotBeChanged_IsCountedAndNotRetriedEveryTick()
    {
        WorkActiveCommsParked();
        _governor.Unreachable.Add(20);

        _resources.Tick();
        _resources.Tick();
        _resources.Tick();

        Assert.Single(_governor.EnterEcoCalls);
        Assert.Equal(1, _resources.Usage.Single(u => u.CategoryId == Comms).UntouchableProcesses);
    }

    [Fact]
    public void Tick_AParkedCategoryWithoutWindows_DoesNothing()
    {
        _table.Add(10, 1, "code");
        _sessions.Add(Work, 10, "code");

        _resources.Tick();

        Assert.Empty(_governor.EnterEcoCalls);
        Assert.Empty(_ledger.Records);
    }

    // ---------------- Memory pressure: Asleep ----------------

    [Fact]
    public void Tick_TightMemory_TrimsATreeParkedLongEnough()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 85;

        _resources.Tick();

        Assert.Equal([20], _governor.TrimCalls);
        Assert.Equal(ResourceLevel.Asleep, _resources.Usage.Single(u => u.CategoryId == Comms).Level);
    }

    [Fact]
    public void Tick_TightMemory_SparesATreeParkedForLessThanTwoMinutes()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromSeconds(100));
        _memory.LoadPercent = 90;

        _resources.Tick();

        Assert.Empty(_governor.TrimCalls);
    }

    [Fact]
    public void Tick_MemoryBelowEighty_NeverTrims()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromMinutes(10));
        _memory.LoadPercent = 79;

        _resources.Tick();

        Assert.Empty(_governor.TrimCalls);
    }

    [Fact]
    public void Tick_Hysteresis_KeepsTheEpisodeGoingUntilBelowSeventy()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 80;
        _resources.Tick();
        Assert.Single(_governor.TrimCalls);

        // 75 % is not "calm again": a trimmed app that woke up and grew is trimmed once more.
        Later(TimeSpan.FromSeconds(61));
        _memory.LoadPercent = 75;
        _resources.Tick();
        Assert.Equal(2, _governor.TrimCalls.Count);

        // Below seventy the episode is over.
        Later(TimeSpan.FromSeconds(61));
        _memory.LoadPercent = 69;
        _resources.Tick();
        Assert.Equal(2, _governor.TrimCalls.Count);
    }

    [Fact]
    public void Tick_Trim_DoesNotRepeatWithinTheRetrimInterval()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 85;
        _resources.Tick();

        Later(TimeSpan.FromSeconds(20));
        _resources.Tick();

        Assert.Single(_governor.TrimCalls);
    }

    [Fact]
    public void Tick_TightMemory_TrimsTheLongestParkedFirst_AndStopsOnceMemoryIsRelieved()
    {
        _table.Add(10, 1, "code").Add(20, 1, "slack").Add(30, 1, "figma");
        _sessions.Add(Work, 10, "code");
        _sessions.Add(Comms, 20, "slack");
        _resources.Tick();
        Later(TimeSpan.FromMinutes(1));
        _sessions.Add(Design, 30, "figma");
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 90;
        _governor.OnTrim = _ => _memory.LoadPercent = 72;

        _resources.Tick();

        Assert.Equal([20], _governor.TrimCalls);
    }

    [Fact]
    public void Tick_ACategoryParkedAgain_StartsItsTwoMinutesOver()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _sessions.ActiveCategoryId = Comms;
        _resources.Tick();
        _sessions.ActiveCategoryId = Work;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(30));
        _memory.LoadPercent = 90;

        _resources.Tick();

        Assert.Empty(_governor.TrimCalls);
    }

    [Fact]
    public void Tick_TrimReportsWhatItFreed_OncePerPressureEpisode()
    {
        WorkActiveCommsParked();
        _table.SetBytes(20, 1400);
        _governor.OnTrim = pid => _table.SetBytes(pid, 100);
        var reports = new List<RelievedMemory>();
        _resources.Relieved += reports.Add;
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 85;

        _resources.Tick();
        Later(TimeSpan.FromSeconds(61));
        _table.SetBytes(20, 600);
        _resources.Tick();

        var report = Assert.Single(reports);
        Assert.Equal(1300L * 1024 * 1024, report.FreedBytes);
        Assert.Equal([Comms], report.CategoryIds);
    }

    [Fact]
    public void FreeMemoryNow_TrimsEveryEligibleTree_WhateverTheLoad_AndReportsWhatItFreed()
    {
        _table.Add(10, 1, "code").Add(20, 1, "slack", mb: 800).Add(30, 1, "figma", mb: 600);
        _sessions.Add(Work, 10, "code");
        _sessions.Add(Comms, 20, "slack");
        _sessions.Add(Design, 30, "figma");
        _governor.OnTrim = pid => _table.SetBytes(pid, 50);
        _resources.Tick();
        _memory.LoadPercent = 30;

        var freed = _resources.FreeMemoryNow();

        Assert.Equal([20, 30], _governor.TrimCalls.Order());
        Assert.Equal((750L + 550L) * 1024 * 1024, freed?.FreedBytes);
        Assert.Equal(new[] { Comms, Design }.Order(), freed!.CategoryIds.Order());
    }

    // ---------------- Frozen ----------------

    [Fact]
    public void Tick_AnAllowedApp_IsFrozenOnlyAfterTightMemoryPersistsThirtySeconds()
    {
        WorkActiveCommsParked();
        _settings.Freezable.Add("slack");
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 87;
        _resources.Tick();
        Assert.Empty(_governor.SuspendCalls);

        Later(TimeSpan.FromSeconds(29));
        _resources.Tick();
        Assert.Empty(_governor.SuspendCalls);

        Later(TimeSpan.FromSeconds(2));
        _resources.Tick();
        Assert.Equal([20], _governor.SuspendCalls);
        Assert.Equal(ResourceLevel.Frozen, _resources.Usage.Single(u => u.CategoryId == Comms).Level);
    }

    [Fact]
    public void Tick_AnAppThatIsNotAllowed_IsNeverFrozen()
    {
        WorkActiveCommsParked();
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 97;
        _resources.Tick();
        Later(TimeSpan.FromMinutes(5));
        _resources.Tick();

        Assert.Empty(_governor.SuspendCalls);
    }

    [Fact]
    public void Tick_TightMemoryThatRelaxesBelowEightyFive_NeverFreezes()
    {
        WorkActiveCommsParked();
        _settings.Freezable.Add("slack");
        _resources.Tick();
        Later(TimeSpan.FromMinutes(3));
        _memory.LoadPercent = 87;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(20));
        _memory.LoadPercent = 78;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(20));
        _memory.LoadPercent = 87;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(20));
        _resources.Tick();

        Assert.Empty(_governor.SuspendCalls);
    }

    [Fact]
    public void Tick_ACategoryThatFreezesImmediately_IsFrozenFiveSecondsAfterParking_WithoutPressure()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _memory.LoadPercent = 30;
        _resources.Tick();
        Assert.Empty(_governor.SuspendCalls);

        Later(TimeSpan.FromSeconds(6));
        _resources.Tick();

        Assert.Equal([20], _governor.SuspendCalls);
    }

    [Fact]
    public void Tick_ACategoryThatFreezesWhenTight_WaitsForPressure()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.WhenTight;
        _resources.Tick();
        Later(TimeSpan.FromMinutes(5));
        _resources.Tick();
        Assert.Empty(_governor.SuspendCalls);

        _memory.LoadPercent = 90;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(31));
        _resources.Tick();

        Assert.Equal([20], _governor.SuspendCalls);
    }

    [Fact]
    public void Tick_AProcessWithWindowsInTwoCategories_IsFrozenOnlyIfEveryWindowIsAllowed()
    {
        _table.Add(10, 1, "code").Add(20, 1, "browser");
        _sessions.Add(Work, 10, "code");
        _sessions.Add(Comms, 20, "browser");
        _sessions.Add(Design, 20, "browser");
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(10));

        _resources.Tick();

        Assert.Empty(_governor.SuspendCalls);
    }

    [Fact]
    public void Tick_WhenAudioCannotBeRead_NothingIsFrozen()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _audio.IsReliable = false;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(10));

        _resources.Tick();

        Assert.Empty(_governor.SuspendCalls);
        Assert.Contains(20, _governor.Eco);
    }

    [Fact]
    public void Tick_AFrozenTreeStaysFrozenWhenPressureDrops_AndWakesWhenItsCategoryIsShown()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(6));
        _resources.Tick();
        Assert.Contains(20, _governor.Suspended);

        Later(TimeSpan.FromMinutes(5));
        _memory.LoadPercent = 20;
        _resources.Tick();
        Assert.Contains(20, _governor.Suspended);

        _sessions.ActiveCategoryId = Comms;
        _resources.Tick();
        Assert.DoesNotContain(20, _governor.Suspended);
        Assert.DoesNotContain(20, _governor.Eco);
        Assert.DoesNotContain(_ledger.Records, r => r.ProcessId == 20);
    }

    [Fact]
    public void Tick_TheLedgerNamesAProcessAsFrozenBeforeItIsSuspended()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(6));
        ThrottleRecord? seenAtSuspend = null;
        _governor.OnSuspend = pid => seenAtSuspend = _ledger.Records.FirstOrDefault(r => r.ProcessId == pid);

        _resources.Tick();

        Assert.NotNull(seenAtSuspend);
        Assert.Equal(ResourceLevel.Frozen, seenAtSuspend.Level);
    }

    [Fact]
    public void Tick_AFreezeThatFails_LeavesTheTreeAsleepNotFrozen()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _resources.Tick();
        _governor.Unreachable.Add(20);
        Later(TimeSpan.FromSeconds(6));

        _resources.Tick();

        Assert.NotEqual(ResourceLevel.Frozen, _resources.Usage.Single(u => u.CategoryId == Comms).Level);
        Assert.Empty(_governor.Suspended);
    }

    [Fact]
    public void Wake_ResumesAtOnce_AndLeavesTheTreeAloneForTheHold_ThenItRestsAgain()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(6));
        _resources.Tick();
        Assert.Contains(20, _governor.Suspended);

        _resources.Wake([20], TimeSpan.FromSeconds(20));
        Assert.DoesNotContain(20, _governor.Suspended);
        Assert.DoesNotContain(20, _governor.Eco);

        Later(TimeSpan.FromSeconds(10));
        _resources.Tick();
        Assert.DoesNotContain(20, _governor.Suspended);

        Later(TimeSpan.FromSeconds(11));
        _resources.Tick();
        Assert.Contains(20, _governor.Eco);
    }

    // ---------------- Crash safety ----------------

    [Fact]
    public void Initialize_PutsBackWhatAPreviousRunLeft_AndIgnoresStrangers()
    {
        _table.Add(20, 1, "slack").Add(30, 1, "stranger", startTicks: 77);
        _governor.Suspended.Add(20);
        _governor.Eco.Add(20);
        _governor.Suspended.Add(30);
        _ledger.Records.Add(new ThrottleRecord(20, 1_020, ResourceLevel.Frozen, FakeProcessGovernor.Original));
        // Pid 30 now belongs to a different process than the one the ledger remembers.
        _ledger.Records.Add(new ThrottleRecord(30, 1_030, ResourceLevel.Frozen, FakeProcessGovernor.Original));
        _ledger.Records.Add(new ThrottleRecord(40, 1_040, ResourceLevel.Frozen, FakeProcessGovernor.Original)); // gone

        _resources.Initialize();

        Assert.DoesNotContain(20, _governor.Suspended);
        Assert.DoesNotContain(20, _governor.Eco);
        Assert.Contains(30, _governor.Suspended);
        Assert.Empty(_ledger.Records);
    }

    [Fact]
    public void ReleaseAll_PutsEveryProcessBack()
    {
        WorkActiveCommsParked();
        _settings.Modes[Comms] = FreezeMode.Immediately;
        _resources.Tick();
        Later(TimeSpan.FromSeconds(6));
        _resources.Tick();
        Assert.NotEmpty(_governor.Suspended);

        _resources.ReleaseAll();

        Assert.Empty(_governor.Suspended);
        Assert.Empty(_governor.Eco);
        Assert.Empty(_ledger.Records);
    }

    // ---------------- What a category costs ----------------

    [Fact]
    public void Usage_SumsTheTreeAndAttributesItToTheCategory()
    {
        _table.Add(10, 1, "code", mb: 300).Add(20, 1, "slack", mb: 200).Add(21, 20, "slack", mb: 150).Add(22, 20, "slack", mb: 50);
        _sessions.Add(Work, 10, "code");
        _sessions.Add(Comms, 20, "slack");

        _resources.Tick();

        Assert.Equal(400L * 1024 * 1024, _resources.Usage.Single(u => u.CategoryId == Comms).PrivateBytes);
        Assert.Equal(300L * 1024 * 1024, _resources.Usage.Single(u => u.CategoryId == Work).PrivateBytes);
    }

    [Fact]
    public void Usage_CpuPercentIsTheTimeUsedOverTheIntervalAcrossAllCores()
    {
        WorkActiveCommsParked();
        _resources.Tick();

        Later(TimeSpan.FromSeconds(10));
        _table.SetCpu(10, TimeSpan.FromSeconds(10) * Environment.ProcessorCount / 4);
        _resources.Tick();

        Assert.Equal(25, _resources.Usage.Single(u => u.CategoryId == Work).CpuPercent, 1);
    }

    [Fact]
    public void Usage_ALevelIsOnlyClaimedWhenEveryTreeOfTheCategoryHasIt()
    {
        _table.Add(10, 1, "code").Add(20, 1, "slack").Add(30, 1, "outlook");
        _sessions.Add(Work, 10, "code");
        _sessions.Add(Comms, 20, "slack");
        _sessions.Add(Comms, 30, "outlook");
        _governor.Unreachable.Add(30);

        _resources.Tick();

        Assert.Equal(ResourceLevel.Normal, _resources.Usage.Single(u => u.CategoryId == Comms).Level);
    }

    [Fact]
    public void MemoryLoadPercent_IsWhatTheLastLookSaw()
    {
        _memory.LoadPercent = 74;

        _resources.Tick();

        Assert.Equal(74, _resources.MemoryLoadPercent);
    }

    [Fact]
    public void Tick_RaisesChanged_OnlyWhenSomethingChanged()
    {
        WorkActiveCommsParked();
        var raised = 0;
        _resources.Changed += () => raised++;

        _resources.Tick();
        var afterFirst = raised;
        _resources.Tick();

        Assert.True(afterFirst > 0);
        Assert.Equal(afterFirst, raised);
    }
}
