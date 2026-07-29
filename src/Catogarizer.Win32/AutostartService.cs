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
            return existing is not null && string.Equals(existing.Trim('"'), ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, $"\"{ExePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string ExePath =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Could not determine the current executable's path.");
}
