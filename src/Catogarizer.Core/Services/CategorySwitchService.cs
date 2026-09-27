using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// See <see cref="ICategorySwitchService"/>. Window attribution is
/// per-window (by handle), not per-process - matches the browser case from
/// CONCEPT.md ("Category switching &amp; sessions"): the same exe can have
/// one window tracked in one category and another window tracked in a
/// different one.
///
/// Known accepted race (poll-based, same trade-off as <see cref="AppBlockingService"/>):
/// a window opened by a template launch is attributed to the active
/// category by <see cref="IWindowWatcher"/>'s next poll tick (up to ~350ms
/// later), not synchronously as <see cref="ICategoryActionService.Open"/>
/// returns. Switching away again within that window could leave the
/// just-launched app un-hidden and later mis-attributed. Not fixed here -
/// revisit only if it bites in practice.
/// </summary>
public sealed class CategorySwitchService : ICategorySwitchService, IDisposable
{
    /// <summary>The implicit "Sonstiges" pseudo-category for anything not assigned to a real one.</summary>
    public static readonly Guid Uncategorized = Guid.Empty;

    private readonly IWindowManager _windowManager;
    private readonly IWindowFinder _windowFinder;
    private readonly IWindowWatcher _windowWatcher;
    private readonly ICategoryActionService _categoryActionService;
    private readonly Func<IReadOnlyList<PinnedApp>> _getPinnedApps;
    private readonly Dictionary<Guid, List<OpenWindowInfo>> _sessions = new();
    private readonly object _lock = new();

    public Guid ActiveCategoryId { get; private set; } = Uncategorized;

    public CategorySwitchService(IWindowManager windowManager, IWindowFinder windowFinder, IWindowWatcher windowWatcher,
        ICategoryActionService categoryActionService, Func<IReadOnlyList<PinnedApp>>? getPinnedApps = null)
    {
        _windowManager = windowManager;
        _windowFinder = windowFinder;
        _windowWatcher = windowWatcher;
        _categoryActionService = categoryActionService;
        _getPinnedApps = getPinnedApps ?? (() => []);
    }

    public void Initialize()
    {
        lock (_lock)
        {
            _sessions[Uncategorized] = _windowFinder.FindAllVisibleWindows().Where(w => !IsPinned(w.ProcessName)).ToList();
            ActiveCategoryId = Uncategorized;
        }
        _windowWatcher.WindowAppeared += OnWindowAppeared;
        _windowWatcher.Start();
    }

    public void SwitchTo(Guid categoryId, IReadOnlyList<AppEntry> templateApps)
    {
        List<OpenWindowInfo> outgoing;
        lock (_lock)
        {
            if (categoryId == ActiveCategoryId) return;
            outgoing = _sessions.TryGetValue(ActiveCategoryId, out var s) ? s : [];
        }

        foreach (var window in outgoing.Where(w => _windowManager.IsWindowOpen(w.Handle)))
            _windowManager.Hide(window.Handle);

        List<OpenWindowInfo>? incoming;
        lock (_lock)
        {
            ActiveCategoryId = categoryId;
            if (_sessions.TryGetValue(categoryId, out var existing))
            {
                incoming = existing.Where(w => _windowManager.IsWindowOpen(w.Handle)).ToList();
                _sessions[categoryId] = incoming;
            }
            else
            {
                incoming = null;
            }
        }

        if (incoming is { Count: > 0 })
            foreach (var window in incoming)
                _windowManager.Show(window.Handle);
        else
            _categoryActionService.Open(templateApps);
    }

    private void OnWindowAppeared(OpenWindowInfo window)
    {
        if (IsPinned(window.ProcessName)) return;

        lock (_lock)
        {
            if (!_sessions.TryGetValue(ActiveCategoryId, out var list))
                _sessions[ActiveCategoryId] = list = [];

            if (!list.Any(w => w.Handle == window.Handle))
                list.Add(window);
        }
    }

    private bool IsPinned(string processName) =>
        _getPinnedApps().Any(p => string.Equals(NormalizedProcessName(p.ProcessNameOrPath), processName, StringComparison.OrdinalIgnoreCase));

    private static string NormalizedProcessName(string pattern) => Path.GetFileNameWithoutExtension(pattern.Trim());

    public void Dispose()
    {
        _windowWatcher.WindowAppeared -= OnWindowAppeared;
        _windowWatcher.Stop();
    }
}
