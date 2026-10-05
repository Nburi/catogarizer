using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

/// <summary>The window menu's Pin/Unpin entry: one toggle over the same pin logic Settings and the home use.</summary>
public sealed class PinToggleTests
{
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWindowFinder _windowFinder = new();
    private readonly LibraryService _library;
    private readonly CategorySwitchService _service;
    private readonly CategorySwitcher _switcher;

    public PinToggleTests()
    {
        _library = new LibraryService(new FakeConfigStore(), fileExists: _ => true);
        _service = new CategorySwitchService(_windowManager, _windowFinder, new FakeWindowWatcher(), new FakeCategoryActionService(),
            () => _library.PinnedApps, new FakeClock(), new FakeHiddenWindowStore(), ownProcessId: 4242);
        _switcher = new CategorySwitcher(_library, _service, new FakeAppBlockingService());
    }

    private static OpenWindowInfo Window(int handle, string processName) => new(new IntPtr(handle), "Window", processName, handle);

    [Fact]
    public void TogglePin_WhenNotPinned_PinsAndKeepsTheWindowVisibleThroughASwitch()
    {
        _windowFinder.RunningWindows.Add(Window(1, "spotify"));
        _service.Initialize();

        var pinned = _switcher.TogglePin("Spotify", @"C:\Apps\Spotify.exe", "spotify");
        _switcher.SwitchTo(_library.AddCategory("Deep Work").Id);

        Assert.True(pinned);
        Assert.Equal(@"C:\Apps\Spotify.exe", Assert.Single(_library.PinnedApps).ProcessNameOrPath);
        Assert.DoesNotContain(new IntPtr(1), _windowManager.HideCalls);
    }

    [Fact]
    public void TogglePin_WhenPinned_UnpinsAndTheWindowJoinsTheActiveCategory()
    {
        _windowFinder.RunningWindows.Add(Window(1, "spotify"));
        _service.Initialize();
        _switcher.TogglePin("Spotify", "spotify", "spotify");

        var pinned = _switcher.TogglePin("Spotify", @"C:\Apps\Spotify.exe", "spotify");

        Assert.False(pinned);
        Assert.Empty(_library.PinnedApps);
        Assert.Contains(new IntPtr(1), _service.GetSessions()[CategorySwitchService.Uncategorized].Select(w => w.Handle));
    }

    [Fact]
    public void TogglePin_Twice_EndsUpUnpinned_NeverWithADuplicate()
    {
        _service.Initialize();

        _switcher.TogglePin("Spotify", "spotify", "spotify");
        _switcher.TogglePin("Spotify", "spotify", "spotify");
        _switcher.TogglePin("Spotify", "spotify", "spotify");

        Assert.Single(_library.PinnedApps);
    }

    [Theory]
    [InlineData("spotify", "spotify")]
    [InlineData("Spotify.exe", "spotify")]
    [InlineData(@"C:\Apps\Spotify.exe", "SPOTIFY")]
    public void FindPinned_MatchesByProcessNameWhateverFormTheEntryWasSavedIn(string saved, string process)
    {
        _library.AddPinnedApp("Spotify", saved);

        Assert.NotNull(_switcher.FindPinned(process));
    }

    [Fact]
    public void FindPinned_IsNullForAnAppThatIsNotPinned()
    {
        _library.AddPinnedApp("Spotify", "spotify");

        Assert.Null(_switcher.FindPinned("notepad"));
    }

    [Fact]
    public void TogglePin_WhenPinnedUnderAnotherForm_UnpinsThatEntryInsteadOfAddingASecondOne()
    {
        _library.AddPinnedApp("Spotify", "Spotify.exe");

        var pinned = _switcher.TogglePin("Spotify", @"C:\Apps\Spotify.exe", "spotify");

        Assert.False(pinned);
        Assert.Empty(_library.PinnedApps);
    }
}
