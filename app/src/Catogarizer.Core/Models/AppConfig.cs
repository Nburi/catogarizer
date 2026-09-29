using Catogarizer.Core.Automation;

namespace Catogarizer.Core.Models;

/// <summary>
/// The whole persisted state - root object serialized to/from the config file.
/// </summary>
public sealed class AppConfig
{
    public List<Category> Categories { get; set; } = new();
    public List<AppEntry> Apps { get; set; } = new();
    public List<BlockedApp> BlockedApps { get; set; } = new();
    public List<PinnedApp> PinnedApps { get; set; } = new();
    public List<Trigger> Triggers { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
