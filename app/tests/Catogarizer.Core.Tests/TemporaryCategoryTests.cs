using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class TemporaryCategoryTests
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

    public TemporaryCategoryTests()
    {
        _library = new LibraryService(_store, fileExists: _ => true);
        _switchService = new CategorySwitchService(_windowManager, _windowFinder, _windowWatcher, new FakeCategoryActionService(),
            () => _library.PinnedApps, _clock, new FakeHiddenWindowStore(), ownProcessId: 4242);
        _switcher = new CategorySwitcher(_library, _switchService, new FakeAppBlockingService());
        _temporary = new TemporaryCategoryService(_library, _switcher, _windowManager, _clock);
        _switchService.Initialize();
    }

    private static OpenWindowInfo Window(int handle) => new(new IntPtr(handle), $"Window {handle}", "app", handle);

    private void OpenWindow(int handle) => _windowWatcher.RaiseWindowAppeared(Window(handle));

    private void CloseWindow(int handle) => _windowManager.SimulateClosedExternally(new IntPtr(handle));

    private void TickAfter(TimeSpan elapsed)
    {
        _clock.Now += elapsed;
        _temporary.Tick();
    }

    // ---------------- Library ----------------

    [Fact]
    public void AddTemporaryCategory_BlankName_IsCalledTempOne()
    {
        var category = _library.AddTemporaryCategory("  ");

        Assert.Equal("Temp 1", category.Name);
        Assert.True(category.IsTemporary);
    }

    [Fact]
    public void AddTemporaryCategory_UsesTheLowestFreeNumber()
    {
        _library.AddTemporaryCategory(null);
        var second = _library.AddTemporaryCategory(null);
        _library.AddTemporaryCategory(null);
        _library.DeleteCategory(second.Id);

        Assert.Equal("Temp 2", _library.AddTemporaryCategory(null).Name);
    }

    [Fact]
    public void AddTemporaryCategory_SkipsANameTheUserAlreadyUsed()
    {
        _library.AddCategory("temp 1");

        Assert.Equal("Temp 2", _library.AddTemporaryCategory(null).Name);
    }

    [Fact]
    public void AddTemporaryCategory_GivenName_IsTrimmedAndUsed()
    {
        Assert.Equal("Taxes", _library.AddTemporaryCategory("  Taxes ").Name);
    }

    [Fact]
    public void AddTemporaryCategory_TakenOrReservedName_Throws()
    {
        _library.AddCategory("Comms");

        Assert.Throws<ArgumentException>(() => _library.AddTemporaryCategory("comms"));
        Assert.Throws<ArgumentException>(() => _library.AddTemporaryCategory("Unsorted"));
        Assert.Single(_library.Categories);
    }

    [Fact]
    public void AddTemporaryCategory_GoesToTheEndOfTheShelf()
    {
        _library.AddCategory("A");
        _library.AddCategory("B");

        var temp = _library.AddTemporaryCategory(null);

        Assert.Equal(temp.Id, _library.Categories.OrderBy(c => c.SortOrder).Last().Id);
        Assert.Equal("3", _library.KeyOf(temp.Id));
    }

    [Fact]
    public void Save_LeavesTemporaryCategoriesOutOfTheFile()
    {
        var kept = _library.AddCategory("Comms");
        _library.AddTemporaryCategory(null);

        _library.AddCategory("Deep Work");

        var saved = _store.Load();
        Assert.Equal(["Comms", "Deep Work"], saved.Categories.Select(c => c.Name).ToArray());
        Assert.Contains(saved.Categories, c => c.Id == kept.Id);
        Assert.Equal(3, _library.Categories.Count);
    }

    [Fact]
    public void Save_WithoutTemporaryCategories_StoresTheConfigAsIs()
    {
        _library.AddCategory("Comms");

        Assert.Single(_store.Load().Categories);
    }

    [Fact]
    public void PermanentCategories_ExcludeTemporaryOnes()
    {
        _library.AddCategory("Comms");
        _library.AddTemporaryCategory(null);

        Assert.Equal(["Comms"], _library.PermanentCategories.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void IsTemporary_OnlyForTemporaryCategories()
    {
        var permanent = _library.AddCategory("Comms");
        var temp = _library.AddTemporaryCategory(null);

        Assert.False(_library.IsTemporary(permanent.Id));
        Assert.True(_library.IsTemporary(temp.Id));
        Assert.False(_library.IsTemporary(CategorySwitchService.Uncategorized));
    }

    // ---------------- Start ----------------

    [Fact]
    public void Start_CreatesTheCategoryAndSwitchesToIt()
    {
        Category? announced = null;
        _temporary.Started += c => announced = c;

        var category = _temporary.Start(null);

        Assert.Equal("Temp 1", category.Name);
        Assert.Equal(category.Id, _switcher.ActiveCategoryId);
        Assert.Same(category, announced);
    }

    [Fact]
    public void Start_TakenName_ThrowsAndChangesNothing()
    {
        _library.AddCategory("Comms");

        Assert.Throws<ArgumentException>(() => _temporary.Start("Comms"));

        Assert.Single(_library.Categories);
        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
    }

    // ---------------- The watch ----------------

    [Fact]
    public void Tick_NeverUsedCategory_StaysNoMatterHowLong()
    {
        var category = _temporary.Start(null);

        TickAfter(TimeSpan.FromHours(2));

        Assert.True(_library.IsTemporary(category.Id));
        Assert.Equal(category.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Tick_LastWindowClosed_StaysDuringTheGracePeriod()
    {
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);

        TickAfter(TemporaryCategoryService.GracePeriod - TimeSpan.FromSeconds(1));
        TickAfter(TimeSpan.FromMilliseconds(500));

        Assert.True(_library.IsTemporary(category.Id));
    }

    [Fact]
    public void Tick_LastWindowClosed_VanishesAfterTheGracePeriodAndGoesBack()
    {
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);
        TickAfter(TimeSpan.Zero);

        TickAfter(TemporaryCategoryService.GracePeriod);

        Assert.DoesNotContain(_library.Categories, c => c.Id == category.Id);
        Assert.Equal(comms.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Tick_Vanishing_ReportsWhereTheAppWent()
    {
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);
        var category = _temporary.Start(null);
        (Category Category, SwitchResult? Result)? vanished = null;
        _temporary.Vanished += (c, r) => vanished = (c, r);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);
        TickAfter(TimeSpan.Zero);

        TickAfter(TemporaryCategoryService.GracePeriod);

        Assert.NotNull(vanished);
        Assert.Equal(category.Id, vanished.Value.Category.Id);
        Assert.Equal(comms.Id, vanished.Value.Result?.CategoryId);
    }

    [Fact]
    public void Tick_WithoutAnyPreviousCategory_GoesToUnsorted()
    {
        _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);
        TickAfter(TimeSpan.Zero);

        TickAfter(TemporaryCategoryService.GracePeriod);

        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
        Assert.Empty(_library.Categories);
    }

    [Fact]
    public void Tick_WhenThePreviousCategoryWasDeleted_GoesToUnsorted()
    {
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);
        _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        _library.DeleteCategory(comms.Id);
        CloseWindow(11);
        TickAfter(TimeSpan.Zero);

        TickAfter(TemporaryCategoryService.GracePeriod);

        Assert.Equal(CategorySwitchService.Uncategorized, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Tick_AWindowOpeningDuringTheGracePeriod_KeepsTheCategory()
    {
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);
        TickAfter(TimeSpan.FromSeconds(2));
        OpenWindow(12);
        TickAfter(TimeSpan.Zero);

        TickAfter(TimeSpan.FromSeconds(10));

        Assert.True(_library.IsTemporary(category.Id));
        Assert.Equal(category.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Tick_GraceStartsOverWhenTheCategoryWasRefilled()
    {
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);
        TickAfter(TimeSpan.FromSeconds(2));
        OpenWindow(12);
        TickAfter(TimeSpan.Zero);
        CloseWindow(12);
        TickAfter(TimeSpan.Zero);

        TickAfter(TimeSpan.FromSeconds(2));

        Assert.True(_library.IsTemporary(category.Id));
    }

    [Fact]
    public void Tick_ParkedCategoryWhoseWindowsAllClosed_VanishesWithoutMovingTheUser()
    {
        var comms = _library.AddCategory("Comms");
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        _switcher.SwitchTo(comms.Id);
        CloseWindow(11);
        TickAfter(TimeSpan.Zero);

        TickAfter(TemporaryCategoryService.GracePeriod);

        Assert.DoesNotContain(_library.Categories, c => c.Id == category.Id);
        Assert.Equal(comms.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Tick_ParkedCategoryWithWindows_Stays()
    {
        var comms = _library.AddCategory("Comms");
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        _switcher.SwitchTo(comms.Id);

        TickAfter(TimeSpan.FromHours(1));

        Assert.True(_library.IsTemporary(category.Id));
    }

    [Fact]
    public void Tick_VanishedCategory_NoLongerHasAKey()
    {
        var comms = _library.AddCategory("Comms");
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        CloseWindow(11);
        TickAfter(TimeSpan.Zero);
        TickAfter(TemporaryCategoryService.GracePeriod);

        Assert.Null(_library.KeyOf(category.Id));
        Assert.Equal("1", _library.KeyOf(comms.Id));
    }

    [Fact]
    public void Tick_WithoutTemporaryCategories_DoesNothing()
    {
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);

        TickAfter(TimeSpan.FromHours(1));

        Assert.Equal(comms.Id, _switcher.ActiveCategoryId);
    }

    // ---------------- Closing it by hand ----------------

    [Fact]
    public void Close_WithWindows_AsksEachOneToCloseGracefully()
    {
        var category = _temporary.Start(null);
        OpenWindow(11);
        OpenWindow(12);
        TickAfter(TimeSpan.Zero);

        _temporary.Close(category.Id);

        Assert.Contains(new IntPtr(11), _windowManager.CloseGracefulCalls);
        Assert.Contains(new IntPtr(12), _windowManager.CloseGracefulCalls);
        Assert.Empty(_windowManager.ForceKillCalls);
    }

    [Fact]
    public void Close_WithWindows_VanishesAtOnceWhenTheyAreGone_WithoutTheGracePeriod()
    {
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        _temporary.Close(category.Id);

        TickAfter(TimeSpan.Zero);

        Assert.DoesNotContain(_library.Categories, c => c.Id == category.Id);
        Assert.Equal(comms.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Close_AWindowThatRefusesToClose_KeepsTheCategory()
    {
        _windowManager.GracefulCloseWorks = false;
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);

        _temporary.Close(category.Id);
        TickAfter(TimeSpan.FromMinutes(1));

        Assert.True(_library.IsTemporary(category.Id));
        Assert.Equal(category.Id, _switcher.ActiveCategoryId);
        Assert.Empty(_windowManager.ForceKillCalls);
    }

    [Fact]
    public void Close_WithoutWindows_VanishesRightAway()
    {
        var comms = _library.AddCategory("Comms");
        _switcher.SwitchTo(comms.Id);
        var category = _temporary.Start(null);

        _temporary.Close(category.Id);

        Assert.DoesNotContain(_library.Categories, c => c.Id == category.Id);
        Assert.Equal(comms.Id, _switcher.ActiveCategoryId);
    }

    [Fact]
    public void Close_ParkedCategory_KeepsItsWindowsInUnsorted()
    {
        var comms = _library.AddCategory("Comms");
        var category = _temporary.Start(null);
        OpenWindow(11);
        TickAfter(TimeSpan.Zero);
        _switcher.SwitchTo(comms.Id);

        _temporary.Close(category.Id);

        Assert.DoesNotContain(_library.Categories, c => c.Id == category.Id);
        Assert.Empty(_windowManager.CloseGracefulCalls);
        Assert.Contains(_switcher.GetSessions()[CategorySwitchService.Uncategorized], w => w.Handle == new IntPtr(11));
    }

    [Fact]
    public void Close_APermanentCategory_IsIgnored()
    {
        var comms = _library.AddCategory("Comms");

        _temporary.Close(comms.Id);

        Assert.Contains(_library.Categories, c => c.Id == comms.Id);
    }
}
