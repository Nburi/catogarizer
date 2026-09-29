using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// App actions: open (launch-or-adopt-already-running, then position if a
/// placement was captured) for a whole template or one app, and per-app
/// minimize and close (graceful, falling back to force-kill if the app doesn't
/// close itself in time).
/// </summary>
public interface ICategoryActionService
{
    CategoryActionResult Open(IReadOnlyList<AppEntry> apps);

    AppActionResult OpenApp(AppEntry app);
    AppActionResult MinimizeApp(AppEntry app);
    AppActionResult CloseApp(AppEntry app);
}
