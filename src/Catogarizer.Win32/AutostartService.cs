using System.Diagnostics;
using Catogarizer.Core.Services;
using Microsoft.Win32;

namespace Catogarizer.Win32;

/// <summary>Per-user autostart via the Registry Run key - no elevation needed.</summary>
public sealed class AutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Catogarizer";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var existing = key?.GetValue(ValueName) as string;
            // Only the path portion is compared (not the full command, "start" and all) so a
            // Run key written before the "start" arg existed still reads as enabled - it just
            // gets rewritten with "start" the next time the user (re)toggles this setting.
            return existing is not null && string.Equals(ExtractPath(existing), ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, $"\"{ExePath}\" start");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Strips a trailing " start" (or any other args) off a quoted "&lt;path&gt; [args]" command string.</summary>
    private static string ExtractPath(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 0) return trimmed[1..closingQuote];
        }
        return trimmed;
    }

    private static string ExePath =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Could not determine the current executable's path.");
}
