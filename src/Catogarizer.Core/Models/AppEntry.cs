namespace Catogarizer.Core.Models;

/// <summary>
/// An app that can be added to one or more categories. Placement is null
/// until the user has captured one via the "grab, don't type" flow -
/// until then the app just launches at whatever position it opens itself.
/// </summary>
public sealed class AppEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public WindowRect? Placement { get; set; }
}
