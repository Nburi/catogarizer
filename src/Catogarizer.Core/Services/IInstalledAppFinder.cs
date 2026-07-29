namespace Catogarizer.Core.Services;

public sealed record InstalledApp(string Name, string ExecutablePath);

public interface IInstalledAppFinder
{
    /// <summary>
    /// Enumerates apps discoverable the way pressing the Windows key does:
    /// Start Menu shortcuts primarily, registry Uninstall entries as a
    /// secondary source for anything a shortcut didn't catch.
    /// </summary>
    IReadOnlyList<InstalledApp> FindInstalledApps();
}
