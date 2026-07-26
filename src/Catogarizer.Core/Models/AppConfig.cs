namespace Catogarizer.Core.Models;

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public List<Category> Categories { get; set; } = new();
    public List<BlockedApp> BlockedApps { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
