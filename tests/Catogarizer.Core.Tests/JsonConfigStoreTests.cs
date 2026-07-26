using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using Xunit;

namespace Catogarizer.Core.Tests;

public class JsonConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public JsonConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"catogarizer-tests-{Guid.NewGuid():N}");
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultConfig_WhenFileDoesNotExist()
    {
        var store = new JsonConfigStore(_configPath);

        var config = await store.LoadAsync();

        Assert.Empty(config.Categories);
        Assert.Empty(config.BlockedApps);
        Assert.Equal(1, config.SchemaVersion);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsData()
    {
        var store = new JsonConfigStore(_configPath);
        var config = new AppConfig
        {
            Categories =
            {
                new Category
                {
                    Name = "Deep Work",
                    Apps =
                    {
                        new AppEntry
                        {
                            Name = "Editor",
                            ExecutablePath = @"C:\Apps\editor.exe",
                            Window = new WindowRect { X = 10, Y = 20, Width = 1000, Height = 700 },
                        },
                    },
                },
            },
            BlockedApps = { new BlockedApp { ProcessName = "steam.exe" } },
        };
        config.Settings.AutostartEnabled = true;

        await store.SaveAsync(config);
        var loaded = await store.LoadAsync();

        var category = Assert.Single(loaded.Categories);
        Assert.Equal("Deep Work", category.Name);
        var app = Assert.Single(category.Apps);
        Assert.Equal(@"C:\Apps\editor.exe", app.ExecutablePath);
        Assert.Equal(1000, app.Window.Width);
        Assert.True(loaded.Settings.AutostartEnabled);
        Assert.Single(loaded.BlockedApps);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAutomationRules()
    {
        var store = new JsonConfigStore(_configPath);
        var categoryId = Guid.NewGuid();
        var config = new AppConfig
        {
            AutomationRules =
            {
                new AutomationRule
                {
                    Name = "Morning focus",
                    TriggerType = AutomationTriggerType.Scheduled,
                    TriggerConfig = "08:00",
                    Action = AutomationActionType.OpenCategory,
                    TargetCategoryId = categoryId,
                },
            },
        };

        await store.SaveAsync(config);
        var loaded = await store.LoadAsync();

        var rule = Assert.Single(loaded.AutomationRules);
        Assert.Equal("Morning focus", rule.Name);
        Assert.Equal(AutomationTriggerType.Scheduled, rule.TriggerType);
        Assert.Equal("08:00", rule.TriggerConfig);
        Assert.Equal(AutomationActionType.OpenCategory, rule.Action);
        Assert.Equal(categoryId, rule.TargetCategoryId);
        Assert.True(rule.Enabled);
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenFileIsCorrupt()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(_configPath, "{ not valid json ");
        var store = new JsonConfigStore(_configPath);

        await Assert.ThrowsAsync<ConfigCorruptException>(() => store.LoadAsync());
    }
}
