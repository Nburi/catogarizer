using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeCategoryActionService : ICategoryActionService
{
    public List<string> Calls { get; } = [];

    public CategoryActionResult Open(IReadOnlyList<AppEntry> apps)
    {
        Calls.Add($"Open:{string.Join(",", apps.Select(a => a.Name))}");
        return new CategoryActionResult(apps.Select(a => new AppActionResult(a, AppActionOutcome.Opened)).ToList());
    }

    public CategoryActionResult Minimize(IReadOnlyList<AppEntry> apps) =>
        new(apps.Select(a => new AppActionResult(a, AppActionOutcome.Minimized)).ToList());

    public CategoryActionResult Close(IReadOnlyList<AppEntry> apps)
    {
        Calls.Add($"Close:{string.Join(",", apps.Select(a => a.Name))}");
        return new CategoryActionResult(apps.Select(a => new AppActionResult(a, AppActionOutcome.Closed)).ToList());
    }

    public AppActionResult OpenApp(AppEntry app)
    {
        Calls.Add($"OpenApp:{app.Name}");
        return new AppActionResult(app, AppActionOutcome.Opened);
    }

    public AppActionResult MinimizeApp(AppEntry app) => new(app, AppActionOutcome.Minimized);

    public AppActionResult CloseApp(AppEntry app)
    {
        Calls.Add($"CloseApp:{app.Name}");
        return new AppActionResult(app, AppActionOutcome.Closed);
    }
}
