using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Services;

/// <summary>What closing a category did.</summary>
/// <param name="Requested">Windows the category held when it was closed.</param>
/// <param name="StillOpen">Windows that did not go away in time - they ask to save, or the app keeps them in the tray.</param>
public sealed record CloseReport(Guid CategoryId, string CategoryName, int Requested, IReadOnlyList<OpenWindowInfo> StillOpen)
{
    public int Closed => Requested - StillOpen.Count;
}

/// <summary>
/// "Close category" (CONCEPT.md "Resource saving"): every window of the category is asked to close the way a
/// click on its X does - never killed, and never the processes behind them (a chat app may keep running in the
/// tray, which is its own business). An app with unsaved work answers with its "Save?" prompt, so a window that
/// is still there after a few seconds is reported instead of forced.
///
/// An explicit, per-category user action with a confirmation, not part of a switch - the same footing as closing a
/// quick category (PRINCIPLES.md value 1: "closing always needs an explicit action").
/// </summary>
public sealed class CategoryCloseService
{
    /// <summary>How long windows get to go away before the rest is reported.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(200);

    /// <summary>Frozen or slowed-down apps are left alone this long, so they can answer the close request.</summary>
    private static readonly TimeSpan WakeHold = TimeSpan.FromSeconds(20);

    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly IWindowManager _windowManager;
    private readonly TemporaryCategoryService _temporary;
    private readonly ResourceGovernor? _resources;
    private readonly IDelay _delay;

    public CategoryCloseService(LibraryService library, CategorySwitcher switcher, IWindowManager windowManager,
        TemporaryCategoryService temporary, ResourceGovernor? resources = null, IDelay? delay = null)
    {
        _library = library;
        _switcher = switcher;
        _windowManager = windowManager;
        _temporary = temporary;
        _resources = resources;
        _delay = delay ?? new SystemDelay();
    }

    /// <summary>Whether the category can be closed as a whole: everything but the implicit Unsorted.</summary>
    public static bool CanClose(Guid categoryId) => categoryId != CategorySwitchService.Uncategorized;

    /// <summary>
    /// Blocks for up to <see cref="Patience"/> while windows close - call it off the UI thread. Null for Unsorted.
    /// </summary>
    public CloseReport? Close(Guid categoryId)
    {
        if (!CanClose(categoryId)) return null;

        var name = _library.NameOf(categoryId);
        var isQuick = _library.IsTemporary(categoryId);
        var windows = _switcher.GetSessions().TryGetValue(categoryId, out var list) ? list : [];
        if (windows.Count == 0)
        {
            if (isQuick) _temporary.Close(categoryId);
            return new CloseReport(categoryId, name, 0, []);
        }

        // A quick category goes as soon as its last window is gone, whether it is the active one or parked.
        if (isQuick) _temporary.ExpectClosed(categoryId);

        // A frozen app cannot answer a close request, and a slowed-down one answers slowly.
        _resources?.Wake(windows.Select(w => w.ProcessId).Distinct(), WakeHold);
        foreach (var window in windows) _windowManager.CloseGraceful(window.Handle);

        var remaining = windows.ToList();
        for (var waited = TimeSpan.Zero; waited < Patience; waited += Step)
        {
            remaining = remaining.Where(w => _windowManager.IsWindowOpen(w.Handle)).ToList();
            if (remaining.Count == 0) break;
            _delay.Wait(Step);
        }
        remaining = remaining.Where(w => _windowManager.IsWindowOpen(w.Handle)).ToList();

        // Nobody stays in an empty category: back to where they came from (a quick category does this on its own).
        if (remaining.Count == 0 && !isQuick && _switcher.ActiveCategoryId == categoryId)
            _switcher.SwitchTo(ReturnTarget(categoryId));

        return new CloseReport(categoryId, name, windows.Count, remaining);
    }

    /// <summary>
    /// The category the user came from - but only if there is something parked there. A category without a
    /// session would launch its template apps, and closing a category must never open anything.
    /// </summary>
    private Guid ReturnTarget(Guid leaving)
    {
        if (_switcher.PreviousCategoryId is { } previous && previous != leaving
            && (previous == CategorySwitchService.Uncategorized || _library.Categories.Any(c => c.Id == previous))
            && _switcher.GetSessions().TryGetValue(previous, out var parked) && parked.Count > 0)
            return previous;
        return CategorySwitchService.Uncategorized;
    }
}
