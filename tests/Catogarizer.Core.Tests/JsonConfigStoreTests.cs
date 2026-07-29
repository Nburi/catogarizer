using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Tests;

public sealed class JsonConfigStoreTests : IDisposable
{
    private readonly string _path;

    public JsonConfigStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"catogarizer-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyConfig()
    {
        var store = new JsonConfigStore(_path);

        var config = store.Load();

        Assert.Empty(config.Categories);
        Assert.Empty(config.Apps);
        Assert.Empty(config.BlockedApps);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsCategoriesAndApps()
    {
        var store = new JsonConfigStore(_path);
        var app = new AppEntry { Name = "VS Code", ExecutablePath = @"C:\code.exe", Placement = new WindowRect { OffsetX = 10, OffsetY = 20, Width = 800, Height = 600, MonitorId = "\\\\.\\DISPLAY1" } };
        var category = new Category { Name = "Deep Work", SortOrder = 0, AppIds = { app.Id } };
        var config = new AppConfig { Apps = { app }, Categories = { category } };

        store.Save(config);
        var loaded = store.Load();

        var loadedCategory = Assert.Single(loaded.Categories);
        Assert.Equal("Deep Work", loadedCategory.Name);
        Assert.Equal(app.Id, Assert.Single(loadedCategory.AppIds));

        var loadedApp = Assert.Single(loaded.Apps);
        Assert.Equal("VS Code", loadedApp.Name);
        Assert.NotNull(loadedApp.Placement);
        Assert.Equal(800, loadedApp.Placement!.Width);
        Assert.Equal("\\\\.\\DISPLAY1", loadedApp.Placement.MonitorId);
    }

    [Fact]
    public void Load_WhenFileIsCorrupted_ThrowsConfigCorruptException()
    {
        File.WriteAllText(_path, "{ this is not valid json");
        var store = new JsonConfigStore(_path);

        var ex = Assert.Throws<ConfigCorruptException>(() => store.Load());
        Assert.Contains(_path, ex.Message);
    }

    [Fact]
    public void Save_OverwritesPreviousContent()
    {
        var store = new JsonConfigStore(_path);
        store.Save(new AppConfig { Categories = { new Category { Name = "First" } } });

        store.Save(new AppConfig { Categories = { new Category { Name = "Second" } } });
        var loaded = store.Load();

        var onlyCategory = Assert.Single(loaded.Categories);
        Assert.Equal("Second", onlyCategory.Name);
    }
}
