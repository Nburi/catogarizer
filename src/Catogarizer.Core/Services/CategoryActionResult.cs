namespace Catogarizer.Core.Services;

public sealed record AppActionOutcome(Guid AppEntryId, string AppName, bool Success, string? ErrorMessage);

public sealed record CategoryActionResult(IReadOnlyList<AppActionOutcome> Outcomes)
{
    public bool AllSucceeded => Outcomes.All(o => o.Success);
}
