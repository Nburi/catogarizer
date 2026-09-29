using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;
using Catogarizer.Core.Theming;

namespace Catogarizer.Core.Tests;

public sealed class CategoryHueTests
{
    [Fact]
    public void EveryPaletteHue_HasAName() =>
        Assert.Equal(CategoryHues.Palette.Count, CategoryHues.Names.Distinct().Count());

    [Fact]
    public void Next_WithNothingUsed_IsTheFirstPaletteHue() =>
        Assert.Equal(CategoryHues.Palette[0], CategoryHues.Next([]));

    [Fact]
    public void Next_PicksThePaletteHueFarthestFromEveryUsedOne() =>
        Assert.Equal(70, CategoryHues.Next([260]));

    [Theory]
    [InlineData(10, 350, 20)]
    [InlineData(0, 180, 180)]
    [InlineData(90, 90, 0)]
    public void AngularDistance_WrapsAroundTheWheel(double a, double b, double expected) =>
        Assert.Equal(expected, CategoryHues.AngularDistance(a, b));

    [Fact]
    public void AddCategory_GivesEveryNewCategoryADistinctHue()
    {
        var library = new LibraryService(new FakeConfigStore(), fileExists: _ => true);

        var hues = Enumerable.Range(1, 6).Select(i => library.AddCategory($"C{i}").Hue).ToList();

        Assert.All(hues, h => Assert.NotNull(h));
        Assert.Equal(6, hues.Distinct().Count());
    }

    [Fact]
    public void Load_AssignsHuesToCategoriesSavedBeforeColorsExisted_AndSavesOnce()
    {
        var store = new FakeConfigStore(new AppConfig
        {
            Categories = [new Category { Name = "Deep Work", SortOrder = 0 }, new Category { Name = "Comms", SortOrder = 1 }],
        });

        var library = new LibraryService(store, fileExists: _ => true);

        Assert.All(library.Categories, c => Assert.NotNull(c.Hue));
        Assert.NotEqual(library.Categories[0].Hue, library.Categories[1].Hue);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void Load_WithAllHuesPresent_DoesNotSave()
    {
        var store = new FakeConfigStore(new AppConfig { Categories = [new Category { Name = "Deep Work", Hue = 260 }] });

        _ = new LibraryService(store, fileExists: _ => true);

        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(400, 40)]
    [InlineData(-30, 330)]
    public void SetCategoryHue_NormalizesToTheWheel(double input, double expected)
    {
        var library = new LibraryService(new FakeConfigStore(), fileExists: _ => true);
        var category = library.AddCategory("Deep Work");

        library.SetCategoryHue(category.Id, input);

        Assert.Equal(expected, category.Hue);
    }
}
