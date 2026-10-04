using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class CategoryStateTextTests
{
    private static AppEntry App(string name, string exe) => new() { Name = name, ExecutablePath = $@"C:\apps\{exe}.exe" };
    private static OpenWindowInfo Window(int handle, string title, string process) => new((IntPtr)handle, title, process, handle);

    private static IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> Unsorted(params OpenWindowInfo[] windows) =>
        new Dictionary<Guid, IReadOnlyList<OpenWindowInfo>> { [CategorySwitchService.Uncategorized] = windows };

    [Theory]
    [InlineData(0, 0, 0, false, 0, "Empty")]
    [InlineData(0, 0, 1, false, 0, "Opens 1 app")]
    [InlineData(0, 0, 2, false, 0, "Opens 2 apps")]
    [InlineData(0, 0, 2, false, 2, "2 open in Unsorted")]
    [InlineData(0, 0, 1, false, 1, "1 open in Unsorted")]
    [InlineData(0, 0, 2, false, 1, "Opens 1 app · 1 open in Unsorted")]
    [InlineData(0, 0, 3, false, 1, "Opens 2 apps · 1 open in Unsorted")]
    [InlineData(0, 0, 2, false, 9, "2 open in Unsorted")]
    [InlineData(3, 0, 2, false, 2, "3 parked")]
    [InlineData(0, 1, 2, false, 1, "1 app not found")]
    [InlineData(0, 2, 2, false, 0, "2 apps not found")]
    [InlineData(0, 0, 0, true, 0, "Nothing parked")]
    public void Describe_UsesTheRealTemplateAndWhatAlreadyRuns(int parked, int missing, int template, bool unsorted, int open, string expected) =>
        Assert.Equal(expected, CategoryStateText.Describe(parked, missing, template, unsorted, open));

    [Fact]
    public void OpenInUnsorted_CountsTemplateAppsThatAlreadyHaveAWindow()
    {
        var apps = new[] { App("Notepad", "notepad"), App("Charmap", "charmap"), App("Calc", "calc") };
        var sessions = Unsorted(Window(1, "Untitled - Notepad", "notepad"), Window(2, "Explorer", "explorer"));

        Assert.Equal(1, CategoryStateText.OpenInUnsorted(apps, sessions));
    }

    [Fact]
    public void OpenInUnsorted_CountsAWindowOnlyOncePerApp()
    {
        // Two apps share one browser process; one window can't satisfy both.
        var apps = new[] { App("Mail", "msedge_proxy"), App("Todo", "msedge_proxy") };
        var sessions = Unsorted(Window(1, "Mail", "msedge"));

        Assert.Equal(1, CategoryStateText.OpenInUnsorted(apps, sessions));
    }

    [Fact]
    public void OpenInUnsorted_IsZeroWithoutUnsortedWindows() =>
        Assert.Equal(0, CategoryStateText.OpenInUnsorted([App("Notepad", "notepad")], new Dictionary<Guid, IReadOnlyList<OpenWindowInfo>>()));

    [Fact]
    public void MatchEach_ReturnsAtMostOneWindowPerAppAndSkipsAppsWithoutOne()
    {
        var apps = new[] { App("A", "a"), App("B", "b") };
        var windows = new[] { Window(1, "A one", "a"), Window(2, "A two", "a") };

        var matched = AppWindowMatcher.MatchEach(apps, windows);

        Assert.Single(matched);
        Assert.Equal((IntPtr)1, matched[0].Handle);
    }
}
