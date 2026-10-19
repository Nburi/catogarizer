using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// Quick categories (CONCEPT.md "Temporary categories"): made in a second, gone on their own.
/// The watch over them starts with their first window - a category nothing ever lived in is not
/// removed behind the user's back - and ends the moment the last window has been closed for
/// <see cref="GracePeriod"/>, so closing one window and opening the next keeps the category.
///
/// Polling, like every other watcher here (STACK.md): a timer that exists only while at least one
/// temporary category does, so an idle app stays idle (PRINCIPLES.md, value 6).
/// </summary>
public sealed class TemporaryCategoryService : IDisposable
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private sealed class Watch
    {
        /// <summary>A window has been seen in the category at least once.</summary>
        public bool Armed;
        public bool CloseRequested;
        public DateTime? EmptySince;
    }

    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly IWindowManager _windowManager;
    private readonly IClock _clock;
    private readonly bool _autoPoll;
    private readonly Dictionary<Guid, Watch> _watches = new();
    private readonly object _gate = new();
    private Timer? _timer;

    /// <param name="autoPoll">Runs <see cref="Tick"/> on a timer while temporary categories exist. Tests drive Tick by hand.</param>
    public TemporaryCategoryService(LibraryService library, CategorySwitcher switcher, IWindowManager windowManager,
        IClock? clock = null, bool autoPoll = false)
    {
        _library = library;
        _switcher = switcher;
        _windowManager = windowManager;
        _clock = clock ?? new SystemClock();
        _autoPoll = autoPoll;
    }

    /// <summary>A quick category was created and is now the active one. Can fire on any thread.</summary>
    public event Action<Category>? Started;

    /// <summary>
    /// A quick category removed itself. The result is where the app went if it was the active category
    /// (null if it was parked, nothing moved). The category is already gone from the library. Can fire on any thread.
    /// </summary>
    public event Action<Category, SwitchResult?>? Vanished;

    public bool IsTemporary(Guid categoryId) => _library.IsTemporary(categoryId);

    /// <summary>Creates the category (a blank <paramref name="name"/> becomes "Temp N") and switches to it.</summary>
    /// <exception cref="ArgumentException">The name is invalid or already taken.</exception>
    public Category Start(string? name)
    {
        Category category;
        lock (_gate)
        {
            category = _library.AddTemporaryCategory(name);
            _watches[category.Id] = new Watch();
            EnsurePolling();
        }

        _switcher.SwitchTo(category.Id);
        Started?.Invoke(category);
        return category;
    }

    /// <summary>
    /// The user closes the category. With windows in it (and it active, so their save prompts can be
    /// seen) they are asked to close; the category goes as soon as the last one is gone, and stays
    /// while one refuses. Without windows, or when it is not the active category, it goes now and any
    /// window left in it moves to Unsorted - nothing is ever stranded.
    /// </summary>
    public void Close(Guid categoryId)
    {
        lock (_gate)
        {
            if (_library.Categories.FirstOrDefault(c => c.Id == categoryId && c.IsTemporary) is not { } category) return;

            var windows = _switcher.GetSessions().TryGetValue(categoryId, out var list) ? list : [];
            if (windows.Count == 0 || _switcher.ActiveCategoryId != categoryId)
            {
                Vanish(category);
                return;
            }

            WatchOf(categoryId).CloseRequested = true;
            foreach (var window in windows)
                _windowManager.CloseGraceful(window.Handle);
            EnsurePolling();
        }
    }

    /// <summary>
    /// The category's windows are being closed by someone else (<see cref="CategoryCloseService"/>): it goes as
    /// soon as the last one is gone, with no grace period, whether it is the active category or parked.
    /// </summary>
    public void ExpectClosed(Guid categoryId)
    {
        lock (_gate)
        {
            if (_library.Categories.All(c => c.Id != categoryId || !c.IsTemporary)) return;
            WatchOf(categoryId).CloseRequested = true;
            EnsurePolling();
        }
    }

    /// <summary>One look at every temporary category. Public so tests can drive it with a fake clock.</summary>
    public void Tick()
    {
        lock (_gate)
        {
            var temporary = _library.Categories.Where(c => c.IsTemporary).ToList();
            foreach (var id in _watches.Keys.Where(id => temporary.All(c => c.Id != id)).ToList())
                _watches.Remove(id);
            if (temporary.Count == 0)
            {
                StopPolling();
                return;
            }

            var sessions = _switcher.GetSessions();
            var now = _clock.Now;
            foreach (var category in temporary)
            {
                var watch = WatchOf(category.Id);
                if (sessions.TryGetValue(category.Id, out var windows) && windows.Count > 0)
                {
                    watch.Armed = true;
                    watch.EmptySince = null;
                    continue;
                }

                if (!watch.Armed) continue;

                watch.EmptySince ??= now;
                var grace = watch.CloseRequested ? TimeSpan.Zero : GracePeriod;
                if (now - watch.EmptySince.Value >= grace)
                    Vanish(category);
            }

            if (!_library.Categories.Any(c => c.IsTemporary)) StopPolling();
        }
    }

    /// <summary>Caller holds the gate.</summary>
    private void Vanish(Category category)
    {
        SwitchResult? returned = null;
        if (_switcher.ActiveCategoryId == category.Id)
            returned = _switcher.SwitchTo(ReturnTarget(category.Id));

        // Whatever is still in it (only possible when the user closes an inactive category) falls to Unsorted.
        _switcher.DeleteCategory(category.Id);
        _watches.Remove(category.Id);
        Vanished?.Invoke(category, returned);
    }

    /// <summary>The category the user came from; Unsorted if that one is gone too.</summary>
    private Guid ReturnTarget(Guid leaving) =>
        _switcher.PreviousCategoryId is { } previous && previous != leaving
            && (previous == CategorySwitchService.Uncategorized || _library.Categories.Any(c => c.Id == previous))
            ? previous
            : CategorySwitchService.Uncategorized;

    private Watch WatchOf(Guid categoryId)
    {
        if (!_watches.TryGetValue(categoryId, out var watch))
            _watches[categoryId] = watch = new Watch();
        return watch;
    }

    private void EnsurePolling()
    {
        if (!_autoPoll || _timer is not null) return;
        _timer = new Timer(_ => SafeTick(), null, PollInterval, PollInterval);
    }

    private void StopPolling()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void SafeTick()
    {
        try
        {
            Tick();
        }
        catch (Exception)
        {
            // A failed look must not take the app down; the next one retries.
        }
    }

    public void Dispose()
    {
        lock (_gate) StopPolling();
    }
}
