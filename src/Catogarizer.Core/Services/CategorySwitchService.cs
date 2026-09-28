using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Services;

/// <summary>
/// See <see cref="ICategorySwitchService"/>. Window attribution is
/// per-window (by handle), not per-process - matches the browser case from
/// CONCEPT.md ("Category switching &amp; sessions"): the same exe can have
/// one window tracked in one category and another window tracked in a
/// different one. A window belongs to at most one session at a time.
///
/// Every hidden window is written to <see cref="IHiddenWindowStore"/> before
/// it is hidden, so a crash never leaves it unreachable (PRINCIPLES.md, value 1).
/// </summary>
public sealed class CategorySwitchService : ICategorySwitchService
{
    /// <summary>The implicit "Unsorted" pseudo-category for anything not assigned to a real one.</summary>
    public static readonly Guid Uncategorized = Guid.Empty;

    public const string UncategorizedName = "Unsorted";

    private readonly IWindowManager _windowManager;
    private readonly IWindowFinder _windowFinder;
    private readonly IWindowWatcher _windowWatcher;
    private readonly ICategoryActionService _categoryActionService;
    private readonly Func<IReadOnlyList<PinnedApp>> _getPinnedApps;
    private readonly IClock _clock;
    private readonly IHiddenWindowStore _hiddenStore;
    private readonly int _ownProcessId;

    private readonly Dictionary<Guid, List<OpenWindowInfo>> _sessions = new();
    private readonly Dictionary<IntPtr, OpenWindowInfo> _hidden = new();
    private readonly object _lock = new();
    private readonly object _switchLock = new();

    public Guid ActiveCategoryId { get; private set; } = Uncategorized;
    public Guid? PreviousCategoryId { get; private set; }
    public DateTime ActiveSince { get; private set; }

    public event Action? StateChanged;

    public CategorySwitchService(IWindowManager windowManager, IWindowFinder windowFinder, IWindowWatcher windowWatcher,
        ICategoryActionService categoryActionService, Func<IReadOnlyList<PinnedApp>>? getPinnedApps = null,
        IClock? clock = null, IHiddenWindowStore? hiddenStore = null, int? ownProcessId = null)
    {
        _windowManager = windowManager;
        _windowFinder = windowFinder;
        _windowWatcher = windowWatcher;
        _categoryActionService = categoryActionService;
        _getPinnedApps = getPinnedApps ?? (() => []);
        _clock = clock ?? new SystemClock();
        _hiddenStore = hiddenStore ?? new NullHiddenWindowStore();
        _ownProcessId = ownProcessId ?? Environment.ProcessId;
    }

    public void Initialize()
    {
        RecoverWindowsHiddenByAPreviousRun();
        lock (_lock)
        {
            _sessions[Uncategorized] = _windowFinder.FindAllAppWindows().Where(IsTrackable).ToList();
            ActiveCategoryId = Uncategorized;
            ActiveSince = _clock.Now;
        }
        _windowWatcher.WindowAppeared += OnWindowAppeared;
        _windowWatcher.Start();
    }

    private void RecoverWindowsHiddenByAPreviousRun()
    {
        foreach (var record in _hiddenStore.Load())
        {
            var handle = new IntPtr(record.Handle);
            if (_windowManager.IsWindowOpen(handle)
                && _windowManager.GetProcessId(handle) == record.ProcessId
                && !_windowManager.IsWindowVisible(handle))
            {
                _windowManager.Show(handle);
            }
        }
        TrySaveHidden([]);
    }

    public SwitchResult SwitchTo(Guid categoryId, IReadOnlyList<AppEntry> templateApps)
    {
        lock (_switchLock)
        {
            List<OpenWindowInfo> outgoing;
            lock (_lock)
            {
                if (categoryId == ActiveCategoryId)
                    return new SwitchResult(categoryId, true, OpenWindows(categoryId).Count, []);

                outgoing = OpenWindows(ActiveCategoryId);
                foreach (var window in outgoing) _hidden[window.Handle] = window;
                PersistHidden();
            }

            foreach (var window in outgoing)
                _windowManager.Hide(window.Handle);

            List<OpenWindowInfo> incoming;
            lock (_lock)
            {
                PreviousCategoryId = ActiveCategoryId;
                ActiveCategoryId = categoryId;
                ActiveSince = _clock.Now;
                incoming = OpenWindows(categoryId);
                _sessions[categoryId] = incoming;
            }

            SwitchResult result;
            if (incoming.Count > 0)
            {
                foreach (var window in incoming)
                    _windowManager.Show(window.Handle);

                lock (_lock)
                {
                    foreach (var window in incoming) _hidden.Remove(window.Handle);
                    PersistHidden();
                }
                result = new SwitchResult(categoryId, true, incoming.Count, []);
            }
            else
            {
                var launch = _categoryActionService.Open(templateApps);
                lock (_lock)
                {
                    // Attribute launched windows now rather than on the watcher's next tick, so an
                    // immediate switch away still hides them with the category that opened them.
                    foreach (var appResult in launch.AppResults)
                    {
                        if (appResult.WindowHandle is not { } handle || handle == IntPtr.Zero) continue;
                        var window = new OpenWindowInfo(handle, _windowManager.GetTitle(handle),
                            Path.GetFileNameWithoutExtension(appResult.App.ExecutablePath), _windowManager.GetProcessId(handle));
                        if (IsTrackable(window)) Attribute(categoryId, window);
                    }
                }
                var opened = launch.AppResults.Count(r => r.Outcome == AppActionOutcome.Opened);
                result = new SwitchResult(categoryId, false, opened, launch.Failures);
            }

            StateChanged?.Invoke();
            return result;
        }
    }

    public IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> GetSessions()
    {
        lock (_lock)
        {
            var result = new Dictionary<Guid, IReadOnlyList<OpenWindowInfo>>();
            foreach (var id in _sessions.Keys.ToList())
            {
                var open = OpenWindows(id);
                _sessions[id] = open;
                result[id] = open.Select(w => w with { Title = LiveTitle(w) }).ToList();
            }
            return result;
        }
    }

    public void ShowAllAndReset()
    {
        lock (_switchLock)
        {
            List<IntPtr> hidden;
            lock (_lock) hidden = _hidden.Keys.ToList();

            foreach (var handle in hidden.Where(_windowManager.IsWindowOpen))
                _windowManager.Show(handle);

            lock (_lock)
            {
                var all = _sessions.Keys.ToList().SelectMany(OpenWindows).DistinctBy(w => w.Handle).ToList();
                _sessions.Clear();
                _sessions[Uncategorized] = all;
                _hidden.Clear();
                PersistHidden();

                if (ActiveCategoryId != Uncategorized)
                {
                    PreviousCategoryId = ActiveCategoryId;
                    ActiveCategoryId = Uncategorized;
                    ActiveSince = _clock.Now;
                }
            }
        }
        StateChanged?.Invoke();
    }

    public void ReleaseCategory(Guid categoryId)
    {
        if (categoryId == Uncategorized) return;

        lock (_switchLock)
        {
            List<OpenWindowInfo> toShow = [];
            lock (_lock)
            {
                var released = OpenWindows(categoryId);
                _sessions.Remove(categoryId);

                if (ActiveCategoryId == categoryId)
                {
                    toShow = OpenWindows(Uncategorized);
                    ActiveCategoryId = Uncategorized;
                    ActiveSince = _clock.Now;
                }
                else if (ActiveCategoryId == Uncategorized)
                {
                    toShow = released;
                }

                var unsorted = Session(Uncategorized);
                unsorted.AddRange(released.Where(r => unsorted.All(u => u.Handle != r.Handle)));
                if (PreviousCategoryId == categoryId || PreviousCategoryId == ActiveCategoryId)
                    PreviousCategoryId = null;
            }

            foreach (var window in toShow)
                _windowManager.Show(window.Handle);

            lock (_lock)
            {
                foreach (var window in toShow) _hidden.Remove(window.Handle);
                PersistHidden();
            }
        }
        StateChanged?.Invoke();
    }

    private void OnWindowAppeared(OpenWindowInfo window)
    {
        if (!IsTrackable(window)) return;

        lock (_lock)
        {
            if (Session(ActiveCategoryId).Any(w => w.Handle == window.Handle)) return;
            // New, or a window of a parked session that re-showed itself (e.g. a chat app on a
            // new message): it is visible in the active context now, so it belongs there.
            Attribute(ActiveCategoryId, window);
        }
        StateChanged?.Invoke();
    }

    /// <summary>Moves <paramref name="window"/> into <paramref name="categoryId"/>'s session. Caller holds _lock.</summary>
    private void Attribute(Guid categoryId, OpenWindowInfo window)
    {
        foreach (var session in _sessions.Values)
            session.RemoveAll(w => w.Handle == window.Handle);
        Session(categoryId).Add(window);
        if (_hidden.Remove(window.Handle)) PersistHidden();
    }

    /// <summary>Caller holds _lock. Also forgets closed windows so the hidden ledger doesn't grow stale.</summary>
    private List<OpenWindowInfo> OpenWindows(Guid categoryId)
    {
        var session = Session(categoryId);
        var closed = session.Where(w => !_windowManager.IsWindowOpen(w.Handle)).ToList();
        if (closed.Count > 0)
        {
            session.RemoveAll(w => closed.Contains(w));
            var hiddenRemoved = false;
            foreach (var w in closed) hiddenRemoved |= _hidden.Remove(w.Handle);
            if (hiddenRemoved) PersistHidden();
        }
        return session.ToList();
    }

    private List<OpenWindowInfo> Session(Guid categoryId)
    {
        if (!_sessions.TryGetValue(categoryId, out var session))
            _sessions[categoryId] = session = [];
        return session;
    }

    private string LiveTitle(OpenWindowInfo window)
    {
        var title = _windowManager.GetTitle(window.Handle);
        return string.IsNullOrEmpty(title) ? window.Title : title;
    }

    private bool IsTrackable(OpenWindowInfo window) =>
        window.ProcessId != _ownProcessId && !IsPinned(window.ProcessName);

    private bool IsPinned(string processName) =>
        _getPinnedApps().Any(p => string.Equals(NormalizedProcessName(p.ProcessNameOrPath), processName, StringComparison.OrdinalIgnoreCase));

    private static string NormalizedProcessName(string pattern) => Path.GetFileNameWithoutExtension(pattern.Trim());

    /// <summary>Caller holds _lock.</summary>
    private void PersistHidden() =>
        TrySaveHidden(_hidden.Values.Select(w => new HiddenWindowRecord(w.Handle.ToInt64(), w.ProcessId, w.ProcessName, w.Title)).ToList());

    private void TrySaveHidden(IReadOnlyList<HiddenWindowRecord> records)
    {
        try
        {
            _hiddenStore.Save(records);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Switching keeps working; the in-process ShowAllAndReset on exit still covers the
            // normal shutdown path, only crash recovery loses this one update.
        }
    }

    public void Dispose()
    {
        _windowWatcher.WindowAppeared -= OnWindowAppeared;
        _windowWatcher.Stop();
    }
}
