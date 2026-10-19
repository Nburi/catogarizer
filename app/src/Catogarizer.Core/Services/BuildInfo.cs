namespace Catogarizer.Core.Services;

/// <summary>
/// Which build is running: its version, the commit it was built from and whether it is a development build
/// (as opposed to the installed one). Shown in the window, the Settings and the tray, so nobody has to guess
/// whether they are looking at the installed app or the one just built.
/// </summary>
public sealed record BuildInfo(string Version, string? Commit, bool IsDevelopment)
{
    private const int ShortCommitLength = 7;

    /// <param name="informationalVersion">"1.2.0+7418fc0..." as the SDK stamps it: the version, then "+" and the commit.</param>
    public static BuildInfo From(string? informationalVersion, bool isDevelopment)
    {
        var raw = (informationalVersion ?? "").Trim();
        var plus = raw.IndexOf('+');
        var version = (plus >= 0 ? raw[..plus] : raw).Trim();
        var commit = plus >= 0 ? raw[(plus + 1)..].Trim() : "";

        return new BuildInfo(
            version.Length == 0 ? "unknown" : version,
            commit.Length == 0 ? null : commit[..Math.Min(ShortCommitLength, commit.Length)],
            isDevelopment);
    }

    private string CommitSuffix => Commit is null ? "" : $" ({Commit})";

    /// <summary>One line for the Settings: what runs, spelled out.</summary>
    public string Summary => IsDevelopment ? $"Development build {Version}{CommitSuffix}" : $"Version {Version}";

    /// <summary>The main window's title; only a development build says anything extra.</summary>
    public string WindowTitle => IsDevelopment ? $"Catogarizer - development build{CommitSuffix}" : "Catogarizer";

    /// <summary>The tray tooltip, with the active category's name.</summary>
    public string TrayText(string categoryName) => IsDevelopment ? $"Catogarizer (dev) · {categoryName}" : $"Catogarizer · {categoryName}";

    /// <summary>The small chip in the home's header; null for the installed app, which stays as it was.</summary>
    public string? DevChipText => IsDevelopment ? Commit is null ? "DEV" : $"DEV {Commit}" : null;
}
