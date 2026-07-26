namespace Catogarizer.Core.Models;

public sealed class BlockedApp
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Process name to match, e.g. "steam.exe" (case-insensitive).</summary>
    public string ProcessName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
