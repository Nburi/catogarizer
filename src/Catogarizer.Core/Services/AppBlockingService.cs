using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// Ties the process watcher to per-category blocklists: only the blocklists
/// of currently-active (opened, not yet closed) categories are enforced.
/// A soft block - the process is killed shortly after it starts, not
/// prevented from starting at all (that would need admin rights).
/// </summary>
public sealed class AppBlockingService : IAppBlockingService, IDisposable
{
    private readonly IProcessWatcher _watcher;
    private readonly Action<int> _killProcess;
    private readonly Func<int, string?> _parentProcessName;
    private readonly Dictionary<Guid, IReadOnlyList<BlockedApp>> _activeCategoryBlocklists = new();
    private readonly object _lock = new();

    public event Action<string>? AppBlocked;

    public AppBlockingService(IProcessWatcher watcher, Action<int>? killProcess = null, Func<int, string?>? parentProcessName = null)
    {
        _watcher = watcher;
        _killProcess = killProcess ?? DefaultKillProcess;
        _parentProcessName = parentProcessName ?? (_ => null);
        _watcher.ProcessStarted += OnProcessStarted;
    }

    public void Start() => _watcher.Start();
    public void Stop() => _watcher.Stop();

    public void ActivateCategory(Guid categoryId, IReadOnlyList<BlockedApp> blockedApps)
    {
        lock (_lock) _activeCategoryBlocklists[categoryId] = blockedApps;
    }

    public void DeactivateCategory(Guid categoryId)
    {
        lock (_lock) _activeCategoryBlocklists.Remove(categoryId);
    }

    private void OnProcessStarted(RunningProcessInfo info)
    {
        BlockedApp? match;
        lock (_lock)
        {
            match = _activeCategoryBlocklists.Values.SelectMany(list => list).FirstOrDefault(b => Matches(b, info));
        }
        if (match is null) return;

        // A helper process spawned by an instance that's already running (Chrome renderers, Electron
        // helpers) isn't a fresh start. That instance may be parked in another category, and killing
        // its helpers would crash the session waiting there (PRINCIPLES.md, value 1).
        if (string.Equals(_parentProcessName(info.ProcessId), info.ProcessName, StringComparison.OrdinalIgnoreCase)) return;

        _killProcess(info.ProcessId);
        AppBlocked?.Invoke(match.Name);
    }

    private static bool Matches(BlockedApp blocked, RunningProcessInfo info)
    {
        var pattern = blocked.ProcessNameOrPath.Trim();
        if (pattern.Contains('\\') || pattern.Contains('/'))
            return info.ExecutablePath is not null && string.Equals(info.ExecutablePath, pattern, StringComparison.OrdinalIgnoreCase);

        var patternName = Path.GetFileNameWithoutExtension(pattern);
        return string.Equals(info.ProcessName, patternName, StringComparison.OrdinalIgnoreCase);
    }

    private static void DefaultKillProcess(int pid)
    {
        try { System.Diagnostics.Process.GetProcessById(pid).Kill(); }
        catch { /* already gone, or a protected process we can't touch - nothing more to do */ }
    }

    public void Dispose()
    {
        _watcher.ProcessStarted -= OnProcessStarted;
        _watcher.Stop();
    }
}
