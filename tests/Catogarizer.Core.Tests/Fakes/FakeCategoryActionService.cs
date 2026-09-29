using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeCategoryActionService : ICategoryActionService
{
    public List<string> Calls { get; } = [];

    /// <summary>Window handle an app's Open reports, by app name. Unlisted apps report none.</summary>
    public Dictionary<string, IntPtr> LaunchHandles { get; } = new();

    /// <summary>App names whose Open fails.</summary>
    public HashSet<string> FailingApps { get; } = new();

    /// <summary>Runs at the start of Open, to observe other state at launch time.</summary>
    public Action? OnOpen { get; set; }

    public CategoryActionResult Open(IReadOnlyList<AppEntry> apps)
    {
        OnOpen?.Invoke();
        Calls.Add($"Open:{string.Join(",", apps.Select(a => a.Name))}");
        return new CategoryActionResult(apps.Select(a => FailingApps.Contains(a.Name)
            ? new AppActionResult(a, AppActionOutcome.Failed, $"{a.Name} failed")
            : new AppActionResult(a, AppActionOutcome.Opened, WindowHandle: LaunchHandles.TryGetValue(a.Name, out var h) ? h : null)).ToList());
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
