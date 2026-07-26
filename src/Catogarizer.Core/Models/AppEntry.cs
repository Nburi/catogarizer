namespace Catogarizer.Core.Models;

public sealed class AppEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public WindowRect Window { get; set; } = new();
    public int LaunchDelayMs { get; set; }
    public int Order { get; set; }
}
