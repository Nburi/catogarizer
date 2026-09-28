using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <param name="RestoredSession">True if saved windows came back, false if the template was opened fresh.</param>
/// <param name="WindowCount">Windows now showing for the category (restored or successfully opened).</param>
public sealed record SwitchResult(Guid CategoryId, bool RestoredSession, int WindowCount, IReadOnlyList<AppActionResult> Failures);

/// <summary>
/// The v2 category-switching model (see CONCEPT.md "Category switching &amp;
/// sessions"): at any moment exactly one category is active, including the
/// implicit <see cref="CategorySwitchService.Uncategorized"/> pseudo-category
/// for whatever's open outside any real category. Switching hides the
/// outgoing category's tracked windows (remembered as its session) and
/// either restores the incoming category's own saved session or opens its
/// template fresh.
/// </summary>
public interface ICategorySwitchService : IDisposable
{
    Guid ActiveCategoryId { get; }

    /// <summary>The category that was active before the current one, if there was a switch yet.</summary>
    Guid? PreviousCategoryId { get; }

    DateTime ActiveSince { get; }

    /// <summary>
    /// Raised after a switch, a reset, or a change to any session's window set.
    /// Can fire on a background thread (the window watcher's).
    /// </summary>
    event Action? StateChanged;

    /// <summary>
    /// Shows any windows a previous (crashed) run left hidden, snapshots
    /// what's open into the Uncategorized session and starts watching for
    /// new windows. Call once, at app startup.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Switches to <paramref name="categoryId"/>: hides the active category's
    /// session, then either restores <paramref name="categoryId"/>'s own
    /// saved session or opens <paramref name="templateApps"/> fresh if it
    /// doesn't have one. A no-op if <paramref name="categoryId"/> is already active.
    /// </summary>
    SwitchResult SwitchTo(Guid categoryId, IReadOnlyList<AppEntry> templateApps);

    /// <summary>Still-open windows per category, with their current titles.</summary>
    IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> GetSessions();

    /// <summary>
    /// Emergency exit and shutdown path: shows every window Catogarizer hid,
    /// moves all tracked windows into Uncategorized and makes it active.
    /// </summary>
    void ShowAllAndReset();

    /// <summary>
    /// For a deleted category: its windows move to Uncategorized so they stay
    /// reachable. If it was active, Uncategorized becomes active (and its own
    /// parked windows come back); windows end up visible whenever
    /// Uncategorized is the active category.
    /// </summary>
    void ReleaseCategory(Guid categoryId);

    /// <summary>
    /// After the pinned list changed: windows of now-pinned apps leave every session and are
    /// shown if they were parked, so pinning takes effect at once instead of on the next switch.
    /// </summary>
    void ApplyPinnedApps();
}
