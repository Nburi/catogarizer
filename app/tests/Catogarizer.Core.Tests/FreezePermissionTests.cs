using System.Text.Json;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Resources;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

/// <summary>What the user may decide about freezing: per app, per category, and the master setting.</summary>
public sealed class FreezePermissionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "catogarizer-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeConfigStore _store = new();
    private readonly LibraryService _library;

    public FreezePermissionTests()
    {
        _library = new LibraryService(_store, fileExists: _ => true);
    }

    [Fact]
    public void AddFreezableApp_IsFoundByProcessName_AndAddingItTwiceKeepsOneEntry()
    {
        var first = _library.AddFreezableApp("Slack", @"C:\Apps\Slack\slack.exe");
        var second = _library.AddFreezableApp("Slack again", "slack");

        Assert.Same(first, second);
        Assert.Single(_library.FreezableApps);
        Assert.NotNull(_library.FindFreezableApp("SLACK"));
        Assert.Null(_library.FindFreezableApp("teams"));
    }

    [Fact]
    public void AddFreezableApp_RejectsAnEmptyName()
    {
        Assert.Throws<ArgumentException>(() => _library.AddFreezableApp("  ", "slack"));
    }

    [Fact]
    public void RemoveFreezableApp_TakesThePermissionBack()
    {
        var app = _library.AddFreezableApp("Slack", "slack");

        _library.RemoveFreezableApp(app.Id);

        Assert.Empty(_library.FreezableApps);
        Assert.Empty(_store.Load().FreezableApps);
    }

    [Fact]
    public void SetFreezeMode_IsSavedPerCategory_AndUnsortedAndUnknownNeverFreeze()
    {
        var comms = _library.AddCategory("Comms");

        _library.SetFreezeMode(comms.Id, FreezeMode.Immediately);

        Assert.Equal(FreezeMode.Immediately, _library.FreezeModeOf(comms.Id));
        Assert.Equal(FreezeMode.Never, _library.FreezeModeOf(CategorySwitchService.Uncategorized));
        Assert.Equal(FreezeMode.Never, _library.FreezeModeOf(Guid.NewGuid()));
        Assert.Equal(FreezeMode.Immediately, _store.Load().Categories.Single().FreezeMode);
    }

    [Fact]
    public void ACategoryNeverFreezesUntilTheUserSaysSo_AndAQuickCategoryCannotBeSet()
    {
        var comms = _library.AddCategory("Comms");
        var quick = _library.AddTemporaryCategory(null);

        Assert.Equal(FreezeMode.Never, _library.FreezeModeOf(comms.Id));
        Assert.Equal(FreezeMode.Never, _library.FreezeModeOf(quick.Id));
        Assert.Throws<InvalidOperationException>(() => _library.SetFreezeMode(quick.Id, FreezeMode.Immediately));
    }

    [Fact]
    public void SoundKeepsACategoryAwake_ByDefault_AndTheChoiceIsSavedPerCategory()
    {
        var games = _library.AddCategory("Games");
        var work = _library.AddCategory("Work");

        Assert.True(_library.KeepSoundAwakeOf(games.Id));

        _library.SetKeepSoundAwake(games.Id, false);

        Assert.False(_library.KeepSoundAwakeOf(games.Id));
        Assert.True(_library.KeepSoundAwakeOf(work.Id));
        Assert.False(_store.Load().Categories.Single(c => c.Id == games.Id).KeepSoundAwake);
    }

    [Fact]
    public void SoundAlwaysProtects_UnsortedQuickCategoriesAndUnknownOnes_AndAQuickCategoryCannotBeSet()
    {
        var quick = _library.AddTemporaryCategory(null);

        Assert.True(_library.KeepSoundAwakeOf(CategorySwitchService.Uncategorized));
        Assert.True(_library.KeepSoundAwakeOf(quick.Id));
        Assert.True(_library.KeepSoundAwakeOf(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => _library.SetKeepSoundAwake(quick.Id, false));
    }

    [Fact]
    public void OldConfigsWithoutTheSoundChoice_KeepSoundAwake_AndTheGovernorSettingsFollowTheLibrary()
    {
        var old = JsonSerializer.Deserialize<AppConfig>("""{"Categories":[{"Name":"Old"}]}""")!;
        Assert.True(old.Categories.Single().KeepSoundAwake);

        var settings = new LibraryResourceSettings(_library);
        var games = _library.AddCategory("Games");
        Assert.True(settings.KeepsSoundAwake(games.Id));
        _library.SetKeepSoundAwake(games.Id, false);
        Assert.False(settings.KeepsSoundAwake(games.Id));
    }

    [Fact]
    public void TheConfigFileNamesTheModeInWords_AndOldConfigsWithoutItStayAtNever()
    {
        var path = Path.Combine(_dir, "config.json");
        var store = new JsonConfigStore(path);
        var library = new LibraryService(store, fileExists: _ => true);
        var comms = library.AddCategory("Comms");
        library.SetFreezeMode(comms.Id, FreezeMode.WhenTight);
        library.AddFreezableApp("Slack", "slack");

        var json = File.ReadAllText(path);
        Assert.Contains("\"FreezeMode\": \"WhenTight\"", json);
        Assert.Equal(FreezeMode.WhenTight, new JsonConfigStore(path).Load().Categories.Single().FreezeMode);

        var old = JsonSerializer.Deserialize<AppConfig>("""{"Categories":[{"Name":"Old"}]}""")!;
        Assert.Equal(FreezeMode.Never, old.Categories.Single().FreezeMode);
        Assert.True(old.Settings.SaveResources);
    }

    [Fact]
    public void TheGovernorSettings_FollowTheLibraryLive()
    {
        var settings = new LibraryResourceSettings(_library);
        var comms = _library.AddCategory("Comms");
        _library.AddPinnedApp("Spotify", "spotify");
        _library.AddFreezableApp("Slack", "slack");
        _library.SetFreezeMode(comms.Id, FreezeMode.WhenTight);

        Assert.True(settings.Enabled);
        Assert.True(settings.IsPinned("Spotify"));
        Assert.False(settings.IsPinned("slack"));
        Assert.True(settings.IsAppFreezable("slack"));
        Assert.Equal(FreezeMode.WhenTight, settings.FreezeModeOf(comms.Id));

        _library.Settings.SaveResources = false;
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void TheThrottleLedger_RoundTripsAMemento_AndSurvivesACorruptFile()
    {
        var path = Path.Combine(_dir, "throttled.json");
        var ledger = new JsonThrottleLedger(path);
        Assert.Empty(ledger.Load());

        var record = new ThrottleRecord(4711, 133_000_000_000, ResourceLevel.Frozen, new EcoMemento(32, 1, 0));
        ledger.Save([record]);
        Assert.Equal(record, Assert.Single(new JsonThrottleLedger(path).Load()));

        File.WriteAllText(path, "{ not json");
        Assert.Empty(ledger.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}

public sealed class ByteSizeTests
{
    [Theory]
    [InlineData(0L, "0 MB")]
    [InlineData(380L * 1024 * 1024, "380 MB")]
    [InlineData(1023L * 1024 * 1024, "1023 MB")]
    [InlineData(1024L * 1024 * 1024, "1.0 GB")]
    [InlineData(1500L * 1024 * 1024, "1.5 GB")]
    public void Format_ReadsLikeTaskManager(long bytes, string expected)
    {
        Assert.Equal(expected, Catogarizer.Core.Resources.ByteSize.Format(bytes));
    }
}

public sealed class SaveResourcesSettingTests
{
    [Fact]
    public void SetSaveResources_IsOnByDefault_AndPersists()
    {
        var store = new Catogarizer.Core.Tests.Fakes.FakeConfigStore();
        var library = new Catogarizer.Core.Services.LibraryService(store, fileExists: _ => true);
        Assert.True(library.Settings.SaveResources);

        library.SetSaveResources(false);

        Assert.False(store.Load().Settings.SaveResources);
    }
}
