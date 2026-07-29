using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public enum AppActionOutcome { Opened, Minimized, Closed, Failed }

public sealed record AppActionResult(AppEntry App, AppActionOutcome Outcome, string? ErrorMessage = null);

public sealed record CategoryActionResult(IReadOnlyList<AppActionResult> AppResults)
{
    public IReadOnlyList<AppActionResult> Failures => AppResults.Where(r => r.Outcome == AppActionOutcome.Failed).ToList();
}
