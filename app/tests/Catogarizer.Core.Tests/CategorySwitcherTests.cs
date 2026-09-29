using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class CategorySwitcherTests
{
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly FakeWindowWatcher _windowWatcher = new();
    private readonly FakeCategoryActionService _categoryActions = new();
    private readonly FakeAppBlockingService _blocking = new();
    private readonly LibraryService _library;
    private readonly CategorySwitchService _service;
    private readonly CategorySwitcher _switcher;

    public CategorySwitcherTests()
    {
        _library = new LibraryService(new FakeConfigStore(), fileExists: _ => true);
        _service = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, _categoryActions,
            () => _library.PinnedApps, new FakeClock(), new FakeHiddenWindowStore(), ownProcessId: 4242);
        _switcher = new CategorySwitcher(_library, _service, _blocking);
        _service.Initialize();
    }

    private Category CategoryWith(string name, string[] apps, string[]? blocked = null)
    {
        var category = _library.AddCategory(name);
        foreach (var app in apps)
            _library.AddAppToCategory(category.Id, _library.AddOrReuseApp(app, $@"C:\{app}.exe").Id);
        foreach (var b in blocked ?? [])
            _library.AddBlockedAppToCategory(category.Id, _library.AddOrReuseBlockedApp(b, b).Id);
        return category;
    }

    [Fact]
    public void SwitchTo_OpensTheCategoryTemplateFromTheLibrary()
    {
        var deepWork = CategoryWith("Deep Work", ["Code", "Obsidian"]);

        _switcher.SwitchTo(deepWork.Id);

        Assert.Contains("Open:Code,Obsidian", _categoryActions.Calls);
        Assert.Equal(deepWork.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void SwitchTo_LiftsTheOutgoingBlocklistBeforeTheNewTemplateLaunches()
    {
        var deepWork = CategoryWith("Deep Work", [], blocked: ["Slack"]);
        var comms = CategoryWith("Comms", ["Slack"]);
        _switcher.SwitchTo(deepWork.Id);

        List<string> blockedDuringLaunch = [];
        _categoryActions.OnOpen = () => blockedDuringLaunch = _blocking.BlockedNames.ToList();
        _switcher.SwitchTo(comms.Id);

        Assert.DoesNotContain("Slack", blockedDuringLaunch);
    }

    [Fact]
    public void SwitchTo_EnforcesTheIncomingBlocklistWhileItsTemplateLaunches()
    {
        var deepWork = CategoryWith("Deep Work", ["Code"], blocked: ["Discord"]);

        List<string> blockedDuringLaunch = [];
        _categoryActions.OnOpen = () => blockedDuringLaunch = _blocking.BlockedNames.ToList();
        _switcher.SwitchTo(deepWork.Id);

        Assert.Contains("Discord", blockedDuringLaunch);
    }

    [Fact]
    public void SwitchTo_UnknownCategory_ReturnsNullAndStaysPut()
    {
        Assert.Null(_switcher.SwitchTo(Guid.NewGuid()));
        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void SwitchBack_ReturnsToThePreviousCategory()
    {
        var deepWork = CategoryWith("Deep Work", []);
        var comms = CategoryWith("Comms", []);
        _switcher.SwitchTo(comms.Id);
        _switcher.SwitchTo(deepWork.Id);

        _switcher.SwitchBack();

        Assert.Equal(comms.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void SwitchBack_WithoutAPreviousSwitch_ReturnsNull() =>
        Assert.Null(_switcher.SwitchBack());

    [Theory]
    [InlineData("deep work")]
    [InlineData("  Deep Work ")]
    public void SwitchByName_IsCaseInsensitiveAndTrimmed(string name)
    {
        var deepWork = CategoryWith("Deep Work", []);

        _switcher.SwitchByName(name);

        Assert.Equal(deepWork.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void SwitchByName_Unsorted_SwitchesToTheImplicitCategory()
    {
        var deepWork = CategoryWith("Deep Work", []);
        _switcher.SwitchTo(deepWork.Id);

        _switcher.SwitchByName("unsorted");

        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void SwitchByName_NoMatch_ReturnsNull() =>
        Assert.Null(_switcher.SwitchByName("Nope"));

    [Fact]
    public void DeleteCategory_Active_LiftsBlockingAndMakesUnsortedActive()
    {
        var deepWork = CategoryWith("Deep Work", [], blocked: ["Discord"]);
        _switcher.SwitchTo(deepWork.Id);

        _switcher.DeleteCategory(deepWork.Id);

        Assert.Empty(_blocking.Active);
        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
        Assert.DoesNotContain(_library.Categories, c => c.Id == deepWork.Id);
    }

    [Fact]
    public void HoldBack_AppliesToTheActiveCategoryRightAway()
    {
        var deepWork = CategoryWith("Deep Work", []);
        _switcher.SwitchTo(deepWork.Id);

        _switcher.HoldBack(deepWork.Id, "Steam", "steam");

        Assert.Contains("Steam", _blocking.BlockedNames);
    }

    [Fact]
    public void StopHoldingBack_LiftsItRightAway()
    {
        var deepWork = CategoryWith("Deep Work", [], blocked: ["Discord"]);
        _switcher.SwitchTo(deepWork.Id);

        _switcher.StopHoldingBack(deepWork.Id, _library.BlockedAppsOf(deepWork)[0].Id);

        Assert.DoesNotContain("Discord", _blocking.BlockedNames);
    }

    [Fact]
    public void ShowAllAndReset_LiftsBlocking()
    {
        var deepWork = CategoryWith("Deep Work", [], blocked: ["Discord"]);
        _switcher.SwitchTo(deepWork.Id);

        _switcher.ShowAllAndReset();

        Assert.Empty(_blocking.Active);
    }

    [Fact]
    public void PinnedAppsFromTheLibrary_AreNeverHidden()
    {
        _windowFinder.RunningWindows.Add(new OpenWindowInfo(new IntPtr(1), "Spotify", "Spotify", 1));
        _library.AddPinnedApp("Spotify", "Spotify.exe");
        var service = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, _categoryActions,
            () => _library.PinnedApps, new FakeClock(), new FakeHiddenWindowStore(), ownProcessId: 4242);
        service.Initialize();

        service.SwitchTo(Guid.NewGuid(), []);

        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
    }
}
