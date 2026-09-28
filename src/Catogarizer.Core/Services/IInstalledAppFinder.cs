namespace Catogarizer.Core.Services;

public sealed record InstalledApp(string Name, string ExecutablePath, string? Arguments = null)
{
    /// <summary>
    /// True for an installed PWA (Edge/Chrome "Install as app") shortcut - its
    /// launch arguments carry a Chromium --app-id, unlike a regular app.
    /// </summary>
    public bool IsPwa => Arguments?.Contains("--app-id=", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>List items are announced by ToString(); a record's default reads out every field.</summary>
    public override string ToString() => IsPwa ? $"{Name}, web app" : Name;
}

public interface IInstalledAppFinder
{
    /// <summary>
    /// Enumerates apps discoverable the way pressing the Windows key does:
    /// Start Menu shortcuts primarily, registry Uninstall entries as a
    /// secondary source for anything a shortcut didn't catch.
    /// </summary>
    IReadOnlyList<InstalledApp> FindInstalledApps();
}
