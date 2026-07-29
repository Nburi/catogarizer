using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// One-click category/app actions: open (launch-or-adopt-already-running,
/// then position if a placement was captured), minimize, and close (graceful,
/// falling back to force-kill if the app doesn't close itself in time).
/// </summary>
public interface ICategoryActionService
{
    CategoryActionResult Open(IReadOnlyList<AppEntry> apps);
    CategoryActionResult Minimize(IReadOnlyList<AppEntry> apps);
    CategoryActionResult Close(IReadOnlyList<AppEntry> apps);

    AppActionResult OpenApp(AppEntry app);
    AppActionResult MinimizeApp(AppEntry app);
    AppActionResult CloseApp(AppEntry app);
}
