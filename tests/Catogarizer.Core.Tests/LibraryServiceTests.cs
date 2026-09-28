using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class LibraryServiceTests
{
    private readonly FakeConfigStore _store = new();
    private readonly LibraryService _service;

    public LibraryServiceTests()
    {
        _service = new LibraryService(_store, fileExists: _ => true);
    }

    [Fact]
    public void AddPinnedApp_AddsPersistsAndReusesTheSameProcess()
    {
        var first = _service.AddPinnedApp("Spotify", "Spotify.exe");
        var second = _service.AddPinnedApp("Spotify again", "spotify.exe");

        Assert.Same(first, second);
        Assert.Single(_service.PinnedApps);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void AddPinnedApp_ReplacesTheListSoReadersHoldingTheOldOneAreUnaffected()
    {
        var before = _service.PinnedApps;

        _service.AddPinnedApp("Spotify", "Spotify.exe");

        Assert.Empty(before);
        Assert.Single(_service.PinnedApps);
    }

    [Fact]
    public void RemovePinnedApp_RemovesIt()
    {
        var pinned = _service.AddPinnedApp("WhatsApp", "WhatsApp.exe");

        _service.RemovePinnedApp(pinned.Id);

        Assert.Empty(_service.PinnedApps);
    }

    [Fact]
    public void AppsOf_ReturnsTheCategorysAppsInOrderSkippingDeletedOnes()
    {
        var category = _service.AddCategory("Deep Work");
        var code = _service.AddApp("Code", @"C:\code.exe");
        var gone = _service.AddApp("Gone", @"C:\gone.exe");
        var obsidian = _service.AddApp("Obsidian", @"C:\obsidian.exe");
        _service.AddAppToCategory(category.Id, code.Id);
        _service.AddAppToCategory(category.Id, gone.Id);
        _service.AddAppToCategory(category.Id, obsidian.Id);
        _service.DeleteApp(gone.Id);

        Assert.Equal(["Code", "Obsidian"], _service.AppsOf(category).Select(a => a.Name));
    }

    [Fact]
    public void AddCategory_AddsAndPersists()
    {
        var category = _service.AddCategory("Deep Work");

        Assert.Single(_service.Categories);
        Assert.Equal("Deep Work", category.Name);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void AddCategory_RejectsDuplicateName()
    {
        _service.AddCategory("Deep Work");

        Assert.Throws<ArgumentException>(() => _service.AddCategory("deep work"));
    }

    [Fact]
    public void RenameCategory_UpdatesName()
    {
        var category = _service.AddCategory("Deep Work");

        _service.RenameCategory(category.Id, "Focus");

        Assert.Equal("Focus", _service.Categories.Single().Name);
    }

    [Fact]
    public void DeleteCategory_RemovesIt()
    {
        var category = _service.AddCategory("Deep Work");

        _service.DeleteCategory(category.Id);

        Assert.Empty(_service.Categories);
    }

    [Fact]
    public void ReorderCategories_UpdatesSortOrder()
    {
        var a = _service.AddCategory("A");
        var b = _service.AddCategory("B");

        _service.ReorderCategories([b.Id, a.Id]);

        Assert.Equal(0, _service.Categories.Single(c => c.Id == b.Id).SortOrder);
        Assert.Equal(1, _service.Categories.Single(c => c.Id == a.Id).SortOrder);
    }

    [Fact]
    public void AddApp_AddsAndPersists()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");

        Assert.Single(_service.Apps);
        Assert.Equal("VS Code", app.Name);
    }

    [Fact]
    public void AddApp_RejectsPathThatDoesNotExist()
    {
        var service = new LibraryService(new FakeConfigStore(), fileExists: _ => false);

        Assert.Throws<ArgumentException>(() => service.AddApp("Ghost", @"C:\definitely\does\not\exist.exe"));
    }

    [Fact]
    public void AddOrReuseApp_ReusesExistingEntryForSamePath()
    {
        var first = _service.AddApp("VS Code", @"C:\code.exe");

        var second = _service.AddOrReuseApp("VS Code (renamed search result)", @"C:\code.exe");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(_service.Apps);
    }

    [Fact]
    public void DeleteApp_RemovesItFromEveryCategoryItWasIn()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");
        var work = _service.AddCategory("Deep Work");
        var comms = _service.AddCategory("Comms");
        _service.AddAppToCategory(work.Id, app.Id);
        _service.AddAppToCategory(comms.Id, app.Id);

        _service.DeleteApp(app.Id);

        Assert.Empty(_service.Apps);
        Assert.DoesNotContain(app.Id, _service.Categories.Single(c => c.Id == work.Id).AppIds);
        Assert.DoesNotContain(app.Id, _service.Categories.Single(c => c.Id == comms.Id).AppIds);
    }

    [Fact]
    public void AddAppToCategory_IsIdempotent()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");
        var category = _service.AddCategory("Deep Work");

        _service.AddAppToCategory(category.Id, app.Id);
        _service.AddAppToCategory(category.Id, app.Id);

        Assert.Single(_service.Categories.Single().AppIds);
    }

    [Fact]
    public void RemoveAppFromCategory_UnlinksButKeepsAppInLibrary()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");
        var category = _service.AddCategory("Deep Work");
        _service.AddAppToCategory(category.Id, app.Id);

        _service.RemoveAppFromCategory(category.Id, app.Id);

        Assert.Empty(_service.Categories.Single().AppIds);
        Assert.Single(_service.Apps);
    }

    [Fact]
    public void SetAppPlacement_SavesPlacementOnTheApp()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");
        var placement = new WindowRect { OffsetX = 10, OffsetY = 20, Width = 800, Height = 600, MonitorId = "\\\\.\\DISPLAY1" };

        _service.SetAppPlacement(app.Id, placement);

        Assert.Equal(placement, _service.Apps.Single().Placement);
    }

    [Fact]
    public void SetAppPlacement_NullClearsIt()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");
        _service.SetAppPlacement(app.Id, new WindowRect { Width = 800, Height = 600 });

        _service.SetAppPlacement(app.Id, null);

        Assert.Null(_service.Apps.Single().Placement);
    }

    [Fact]
    public void AddBlockedApp_AddsAndPersists()
    {
        var blocked = _service.AddBlockedApp("Discord", "discord");

        Assert.Single(_service.BlockedApps);
        Assert.Equal("Discord", blocked.Name);
    }

    [Fact]
    public void AddOrReuseBlockedApp_ReusesExistingEntryForSameValue()
    {
        var first = _service.AddBlockedApp("Discord", "discord");

        var second = _service.AddOrReuseBlockedApp("Discord (renamed)", "discord");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(_service.BlockedApps);
    }

    [Fact]
    public void DeleteBlockedApp_RemovesItFromEveryCategoryItWasIn()
    {
        var blocked = _service.AddBlockedApp("Discord", "discord");
        var category = _service.AddCategory("Deep Work");
        _service.AddBlockedAppToCategory(category.Id, blocked.Id);

        _service.DeleteBlockedApp(blocked.Id);

        Assert.Empty(_service.BlockedApps);
        Assert.DoesNotContain(blocked.Id, _service.Categories.Single().BlockedAppIds);
    }

    [Fact]
    public void AddBlockedAppToCategory_IsIdempotent()
    {
        var blocked = _service.AddBlockedApp("Discord", "discord");
        var category = _service.AddCategory("Deep Work");

        _service.AddBlockedAppToCategory(category.Id, blocked.Id);
        _service.AddBlockedAppToCategory(category.Id, blocked.Id);

        Assert.Single(_service.Categories.Single().BlockedAppIds);
    }

    [Fact]
    public void RemoveBlockedAppFromCategory_UnlinksButKeepsEntryInLibrary()
    {
        var blocked = _service.AddBlockedApp("Discord", "discord");
        var category = _service.AddCategory("Deep Work");
        _service.AddBlockedAppToCategory(category.Id, blocked.Id);

        _service.RemoveBlockedAppFromCategory(category.Id, blocked.Id);

        Assert.Empty(_service.Categories.Single().BlockedAppIds);
        Assert.Single(_service.BlockedApps);
    }

    [Fact]
    public void UpdateSettings_UpdatesAndPersists()
    {
        var saveCountBefore = _store.SaveCount;

        _service.UpdateSettings(startMinimized: true, commandPaletteHotkey: "Ctrl+Alt+K");

        Assert.True(_service.Settings.StartMinimized);
        Assert.Equal("Ctrl+Alt+K", _service.Settings.CommandPaletteHotkey);
        Assert.True(_store.SaveCount > saveCountBefore);
    }

    [Fact]
    public void AddTrigger_AddsAndPersists()
    {
        var trigger = _service.AddTrigger("Morning", TriggerType.Startup);

        Assert.Single(_service.Triggers);
        Assert.Equal("Morning", trigger.Name);
        Assert.Equal(TriggerType.Startup, trigger.Type);
        Assert.True(trigger.IsEnabled);
    }

    [Fact]
    public void AddTrigger_RejectsDuplicateName()
    {
        _service.AddTrigger("Morning", TriggerType.Manual);

        Assert.Throws<ArgumentException>(() => _service.AddTrigger("morning", TriggerType.Manual));
    }

    [Fact]
    public void UpdateTrigger_ReplacesNameTypeScheduleAndActions()
    {
        var app = _service.AddApp("VS Code", @"C:\code.exe");
        var trigger = _service.AddTrigger("Morning", TriggerType.Manual);
        var actions = new List<TriggerAction> { new() { Type = TriggerActionType.OpenApp, AppId = app.Id } };

        _service.UpdateTrigger(trigger.Id, "Evening", TriggerType.Time, "18:00", [DayOfWeek.Monday], actions);

        var updated = _service.Triggers.Single();
        Assert.Equal("Evening", updated.Name);
        Assert.Equal(TriggerType.Time, updated.Type);
        Assert.Equal("18:00", updated.TimeOfDay);
        Assert.Equal([DayOfWeek.Monday], updated.DaysOfWeek);
        Assert.Single(updated.Actions);
    }

    [Fact]
    public void UpdateTrigger_SwitchingAwayFromTime_ClearsScheduleFields()
    {
        var trigger = _service.AddTrigger("Morning", TriggerType.Time);
        _service.UpdateTrigger(trigger.Id, "Morning", TriggerType.Time, "08:00", [DayOfWeek.Monday], []);

        _service.UpdateTrigger(trigger.Id, "Morning", TriggerType.Manual, null, [], []);

        var updated = _service.Triggers.Single();
        Assert.Null(updated.TimeOfDay);
        Assert.Empty(updated.DaysOfWeek);
    }

    [Fact]
    public void UpdateTrigger_RejectsInvalidTimeFormat()
    {
        var trigger = _service.AddTrigger("Morning", TriggerType.Manual);

        Assert.Throws<ArgumentException>(() => _service.UpdateTrigger(trigger.Id, "Morning", TriggerType.Time, "not a time", [], []));
    }

    [Fact]
    public void SetTriggerEnabled_TogglesAndPersists()
    {
        var trigger = _service.AddTrigger("Morning", TriggerType.Manual);

        _service.SetTriggerEnabled(trigger.Id, false);

        Assert.False(_service.Triggers.Single().IsEnabled);
    }

    [Fact]
    public void DeleteTrigger_RemovesIt()
    {
        var trigger = _service.AddTrigger("Morning", TriggerType.Manual);

        _service.DeleteTrigger(trigger.Id);

        Assert.Empty(_service.Triggers);
    }
}
