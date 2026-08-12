namespace Catogarizer.Core.Services;

public sealed record InstalledApp(string Name, string ExecutablePath, string? Arguments = null)
{
    /// <summary>
    /// True for an installed PWA (Edge/Chrome "Install as app") shortcut - its
    /// launch arguments carry a Chromium --app-id, unlike a regular app.
    /// </summary>
    public bool IsPwa => Arguments?.Contains("--app-id=", StringComparison.OrdinalIgnoreCase) == true;
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
