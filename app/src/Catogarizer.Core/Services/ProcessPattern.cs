namespace Catogarizer.Core.Services;

/// <summary>
/// Held-back and pinned apps are stored as either a full exe path or a bare process name
/// ("claude", "Spotify.exe"). One place decides which it is and what process name it means.
/// </summary>
public static class ProcessPattern
{
    public static bool IsPath(string pattern) => pattern.Contains('\\') || pattern.Contains('/');

    /// <summary>The process name the pattern stands for, without folder or ".exe".</summary>
    public static string ProcessName(string pattern) => Path.GetFileNameWithoutExtension(pattern.Trim());
}
