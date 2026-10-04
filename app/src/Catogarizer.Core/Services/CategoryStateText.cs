namespace Catogarizer.Core.Services;

/// <summary>The one wording for "what's there" on a category, shared by the home's tiles, the back card and the palette.</summary>
public static class CategoryStateText
{
    /// <param name="openInUnsorted">
    /// Template apps that already run as Unsorted windows. Switching takes those over instead of
    /// launching them again, so "Opens N apps" would be wrong for them.
    /// </param>
    public static string Describe(int parked, int missing, int templateCount, bool isUnsorted, int openInUnsorted = 0)
    {
        if (parked > 0) return $"{parked} parked";
        if (isUnsorted) return "Nothing parked";
        if (missing > 0) return missing == 1 ? "1 app not found" : $"{missing} apps not found";
        if (templateCount == 0) return "Empty";

        var open = Math.Clamp(openInUnsorted, 0, templateCount);
        var toLaunch = templateCount - open;
        var opens = toLaunch == 1 ? "Opens 1 app" : $"Opens {toLaunch} apps";
        return open == 0 ? opens
            : toLaunch == 0 ? $"{open} open in Unsorted"
            : $"{opens} · {open} open in Unsorted";
    }

    /// <summary>How many of a category's template apps already run as windows in Unsorted.</summary>
    public static int OpenInUnsorted(IEnumerable<Models.AppEntry> templateApps, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions) =>
        sessions.TryGetValue(CategorySwitchService.Uncategorized, out var unsorted)
            ? AppWindowMatcher.MatchEach(templateApps, unsorted).Count
            : 0;
}
