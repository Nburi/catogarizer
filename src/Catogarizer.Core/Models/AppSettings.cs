namespace Catogarizer.Core.Models;

public sealed class AppSettings
{
    public bool AutostartEnabled { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public string Theme { get; set; } = "CyberpunkNeon";
}
