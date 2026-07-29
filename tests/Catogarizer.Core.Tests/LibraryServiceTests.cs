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
}
