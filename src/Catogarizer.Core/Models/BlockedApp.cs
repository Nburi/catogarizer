namespace Catogarizer.Core.Models;

/// <summary>
/// An app to watch for and soft-close while it's blocked. Kept separate
/// from <see cref="AppEntry"/> because a blocked app only needs enough to
/// recognize a running process - no launch args, no placement.
/// </summary>
public sealed class BlockedApp
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProcessNameOrPath { get; set; } = string.Empty;

    public override string ToString() => Name;
}
