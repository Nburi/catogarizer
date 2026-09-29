using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public static class AppWindowMatcher
{
    /// <summary>
    /// A PWA's shortcut launches a short-lived stub (msedge_proxy.exe/chrome_proxy.exe)
    /// that relaunches the real browser and exits - the actual window ends up owned by
    /// the unsuffixed browser process, so that name is included as a second candidate.
    /// </summary>
    public static IReadOnlyList<string> ProcessNameCandidates(AppEntry app)
    {
        var processName = Path.GetFileNameWithoutExtension(app.ExecutablePath);
        return processName.EndsWith("_proxy", StringComparison.OrdinalIgnoreCase)
            ? [processName, processName[..^"_proxy".Length]]
            : [processName];
    }

    /// <summary>
    /// The window most likely to be <paramref name="app"/>'s: a process match whose title also
    /// contains the app's name beats a bare process match (several PWAs share one browser process).
    /// </summary>
    public static OpenWindowInfo? BestMatch(AppEntry app, IEnumerable<OpenWindowInfo> windows)
    {
        var candidates = ProcessNameCandidates(app);
        var byProcess = windows.Where(w => candidates.Contains(w.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
        return byProcess.FirstOrDefault(w => w.Title.Contains(app.Name, StringComparison.OrdinalIgnoreCase))
               ?? byProcess.FirstOrDefault();
    }
}
