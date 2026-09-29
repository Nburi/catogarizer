using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

/// <summary>Name, number key and missing-app lookups that every surface (home, palette, tray, pill) shares.</summary>
public sealed class CategoryLookupTests
{
    private readonly HashSet<string> _missingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly LibraryService _library;

    public CategoryLookupTests()
    {
        _library = new LibraryService(new FakeConfigStore(), fileExists: p => !_missingPaths.Contains(p));
    }

    [Fact]
    public void NameOf_UnsortedAndUnknownIds_ReadUnsorted()
    {
        var comms = _library.AddCategory("Comms");

        Assert.Equal("Comms", _library.NameOf(comms.Id));
        Assert.Equal("Unsorted", _library.NameOf(CategorySwitchService.Uncategorized));
        Assert.Equal("Unsorted", _library.NameOf(Guid.NewGuid()));
    }

    [Fact]
    public void KeyOf_NumbersTheFirstNineInOrder_AndUnsortedIsZero()
    {
        var categories = Enumerable.Range(1, 10).Select(i => _library.AddCategory($"C{i}")).ToList();

        Assert.Equal("1", _library.KeyOf(categories[0].Id));
        Assert.Equal("9", _library.KeyOf(categories[8].Id));
        Assert.Null(_library.KeyOf(categories[9].Id));
        Assert.Equal("0", _library.KeyOf(CategorySwitchService.Uncategorized));
    }

    [Fact]
    public void CategoryIdForKey_IsTheReverseOfKeyOf()
    {
        var first = _library.AddCategory("First");
        var second = _library.AddCategory("Second");

        Assert.Equal(first.Id, _library.CategoryIdForKey(1));
        Assert.Equal(second.Id, _library.CategoryIdForKey(2));
        Assert.Equal(CategorySwitchService.Uncategorized, _library.CategoryIdForKey(0));
        Assert.Null(_library.CategoryIdForKey(3));
    }

    [Fact]
    public void MissingAppCount_CountsAppsWhoseExeIsGone()
    {
        var work = _library.AddCategory("Work");
        foreach (var path in new[] { @"C:\a.exe", @"C:\b.exe", @"C:\c.exe" })
            _library.AddAppToCategory(work.Id, _library.AddApp(Path.GetFileNameWithoutExtension(path), path).Id);

        _missingPaths.Add(@"C:\b.exe");
        _missingPaths.Add(@"C:\c.exe");

        Assert.Equal(2, _library.MissingAppCount(work));
    }

    [Theory]
    [InlineData("claude", false, "claude")]
    [InlineData(" Spotify.exe ", false, "Spotify")]
    [InlineData(@"C:\Apps\Slack\slack.exe", true, "slack")]
    [InlineData("C:/Apps/steam.exe", true, "steam")]
    public void ProcessPattern_TellsPathsFromNames(string pattern, bool isPath, string processName)
    {
        Assert.Equal(isPath, ProcessPattern.IsPath(pattern));
        Assert.Equal(processName, ProcessPattern.ProcessName(pattern));
    }
}
