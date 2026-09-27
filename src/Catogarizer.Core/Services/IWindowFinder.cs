namespace Catogarizer.Core.Services;

public interface IWindowFinder
{
    /// <summary>
    /// Finds the main window of a just-started process. Tries the process's own
    /// window handle first, then falls back to a title-substring scan across
    /// <paramref name="titleCandidates"/> (needed for host-process apps like UWP/
    /// packaged apps whose visible window isn't owned by the launched process,
    /// and whose title is localized - so more than one candidate matters).
    /// </summary>
    IntPtr? FindMainWindow(int processId, IReadOnlyList<string> titleCandidates, TimeSpan timeout);

    /// <summary>
    /// Finds every currently visible top-level window that plausibly belongs to
    /// a target app, whether or not this app launched it - matched by
    /// owning-process name first (unambiguous for normal apps), title substring
    /// as a fallback (needed for host processes that can own windows for several
    /// different packaged apps at once).
    /// </summary>
    IReadOnlyList<OpenWindowInfo> FindAllRunningWindows(IReadOnlyList<string> processNameCandidates, IReadOnlyList<string> titleCandidates);

    /// <summary>
    /// Every currently visible top-level window with a non-empty title, no
    /// filtering - the raw snapshot <see cref="IWindowWatcher"/> diffs
    /// against itself over time to notice new windows appearing.
    /// </summary>
    IReadOnlyList<OpenWindowInfo> FindAllVisibleWindows();
}
