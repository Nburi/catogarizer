using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public sealed class CategoryActionService : ICategoryActionService
{
    private readonly IProcessLauncher _launcher;
    private readonly IWindowManager _windowManager;

    public CategoryActionService(IProcessLauncher launcher, IWindowManager windowManager)
    {
        _launcher = launcher;
        _windowManager = windowManager;
    }

    public async Task<CategoryActionResult> OpenAsync(Category category, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<AppActionOutcome>();
        var ordered = category.Apps.OrderBy(a => a.Order).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var app = ordered[i];
            try
            {
                var running = _launcher.FindRunning(app);
                if (running is { MainWindowHandle: not 0 })
                {
                    _windowManager.Restore(running.MainWindowHandle);
                    _windowManager.MoveResize(running.MainWindowHandle, app.Window);
                }
                else
                {
                    var launched = await _launcher.LaunchAsync(app, cancellationToken);
                    if (launched.MainWindowHandle != 0)
                        _windowManager.MoveResize(launched.MainWindowHandle, app.Window);
                }

                outcomes.Add(new AppActionOutcome(app.Id, app.Name, true, null));
            }
            catch (Exception ex)
            {
                outcomes.Add(new AppActionOutcome(app.Id, app.Name, false, DescribeError(ex, app)));
            }

            if (app.LaunchDelayMs > 0 && i < ordered.Count - 1)
                await Task.Delay(app.LaunchDelayMs, cancellationToken);
        }

        return new CategoryActionResult(outcomes);
    }

    public Task<CategoryActionResult> CloseAsync(Category category, CancellationToken cancellationToken = default)
        => ApplyToRunningApps(category, _windowManager.Close);

    public Task<CategoryActionResult> MinimizeAsync(Category category, CancellationToken cancellationToken = default)
        => ApplyToRunningApps(category, _windowManager.Minimize);

    private Task<CategoryActionResult> ApplyToRunningApps(Category category, Action<nint> action)
    {
        var outcomes = new List<AppActionOutcome>();

        foreach (var app in category.Apps)
        {
            try
            {
                var running = _launcher.FindRunning(app);
                if (running is { MainWindowHandle: not 0 })
                {
                    action(running.MainWindowHandle);
                    outcomes.Add(new AppActionOutcome(app.Id, app.Name, true, null));
                }
                else
                {
                    outcomes.Add(new AppActionOutcome(app.Id, app.Name, true, "Not running."));
                }
            }
            catch (Exception ex)
            {
                outcomes.Add(new AppActionOutcome(app.Id, app.Name, false, DescribeError(ex, app)));
            }
        }

        return Task.FromResult(new CategoryActionResult(outcomes));
    }

    private static string DescribeError(Exception ex, AppEntry app) => ex switch
    {
        FileNotFoundException => $"Executable not found: {app.ExecutablePath}",
        InvalidOperationException => $"Could not start {app.Name}.",
        _ => $"{app.Name}: {ex.Message}",
    };
}
