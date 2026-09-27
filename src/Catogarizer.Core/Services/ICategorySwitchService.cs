using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

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

    /// <summary>
    /// Snapshots whatever's currently open into the Uncategorized session and
    /// starts watching for new windows. Call once, at app startup.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Switches to <paramref name="categoryId"/>: hides the active category's
    /// session, then either restores <paramref name="categoryId"/>'s own
    /// saved session or opens <paramref name="templateApps"/> fresh if it
    /// doesn't have one yet. A no-op if <paramref name="categoryId"/> is
    /// already active.
    /// </summary>
    void SwitchTo(Guid categoryId, IReadOnlyList<AppEntry> templateApps);
}
