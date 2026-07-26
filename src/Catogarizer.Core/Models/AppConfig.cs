using Catogarizer.Core.Automation;

namespace Catogarizer.Core.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public List<Category> Categories { get; set; } = new();
    public List<BlockedApp> BlockedApps { get; set; } = new();
    public AppSettings Settings { get; set; } = new();

    /// <summary>Reserved for future scheduled-automation support; no UI creates these yet and nothing evaluates them.</summary>
    public List<AutomationRule> AutomationRules { get; set; } = new();
}
