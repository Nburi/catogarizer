using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Catogarizer.Win32;

namespace Catogarizer.App.Services;

/// <summary>Human names for running apps ("Microsoft Word", not "WINWORD"), cached per process.</summary>
public static class AppNames
{
    private static readonly ConcurrentDictionary<int, string> ByProcess = new();

    public static string ForProcess(int processId, string processName) =>
        ByProcess.GetOrAdd(processId, pid => ShellIcons.TryGetExecutablePath(pid) is { } path ? ForExecutable(path, processName) : processName);

    public static string ForExecutable(string path, string fallback)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            // Some apps (Windows 11 Notepad) describe themselves as "Notepad.exe".
            if (description?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true) description = description[..^4];
            if (!string.IsNullOrEmpty(description)) return description;
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException)
        {
        }
        return string.IsNullOrWhiteSpace(fallback) ? Path.GetFileNameWithoutExtension(path) : fallback;
    }

    /// <summary>The exe path for a running process, or null (e.g. it just exited).</summary>
    public static string? ExecutableOf(int processId) => ShellIcons.TryGetExecutablePath(processId);
}
