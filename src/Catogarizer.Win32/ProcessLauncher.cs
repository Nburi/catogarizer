using System.Diagnostics;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Win32;

public sealed class ProcessLauncher : IProcessLauncher
{
    private const int WindowPollTimeoutMs = 5000;
    private const int WindowPollIntervalMs = 100;

    public async Task<LaunchedApp> LaunchAsync(AppEntry entry, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(entry.ExecutablePath))
            throw new FileNotFoundException($"Executable not found: {entry.ExecutablePath}", entry.ExecutablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = entry.ExecutablePath,
            Arguments = entry.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(entry.WorkingDirectory)
                ? Path.GetDirectoryName(entry.ExecutablePath) ?? string.Empty
                : entry.WorkingDirectory,
            UseShellExecute = true,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {entry.ExecutablePath}.");

        var handle = await WaitForMainWindowAsync(process, cancellationToken);
        return new LaunchedApp(process.Id, handle, entry.ExecutablePath);
    }

    public LaunchedApp? FindRunning(AppEntry entry)
    {
        var targetName = Path.GetFileNameWithoutExtension(entry.ExecutablePath);
        if (string.IsNullOrEmpty(targetName))
            return null;

        // Matching by name+path only finds the process Windows launched from that exact
        // file. That breaks for two real-world shapes: multi-process apps (Electron/Chromium
        // apps like Discord/Spotify run several same-named processes - renderer, GPU, utility -
        // and only one of them owns the actual window, so "first match wins" can just as
        // easily grab a windowless helper), and Store/alias-launched apps (Spotify.exe under
        // WindowsApps is a stub; the real running process's module path is the resolved
        // package path, which never string-equals the configured alias path). So the exact
        // path is only used as a *preference* between same-named matches, never a hard filter.
        var byName = FindBestMatch(Process.GetProcessesByName(targetName), entry.ExecutablePath);
        if (byName is { MainWindowHandle: not 0 })
            return byName;

        // Some apps (Steam is the clearest example) run their real UI window in a
        // differently-named helper process installed alongside the configured executable -
        // steam.exe itself never owns a window, steamwebhelper.exe does. Fall back to any
        // window-owning process whose module lives under the same install directory.
        var installDir = Path.GetDirectoryName(entry.ExecutablePath);
        if (!string.IsNullOrEmpty(installDir))
        {
            var byDirectory = FindBestMatch(Process.GetProcesses(), targetPath: null, requiredDirectory: installDir);
            if (byDirectory is { MainWindowHandle: not 0 })
                return byDirectory;
        }

        return byName;
    }

    private static LaunchedApp? FindBestMatch(IEnumerable<Process> candidates, string? targetPath, string? requiredDirectory = null)
    {
        LaunchedApp? exactPathWithoutWindow = null;

        foreach (var process in candidates)
        {
            using (process)
            {
                string? modulePath;
                try
                {
                    modulePath = process.MainModule?.FileName;
                }
                catch (Exception)
                {
                    continue; // MainModule is inaccessible for protected/elevated processes.
                }

                if (modulePath is null)
                    continue;

                // Compare with a trailing separator so "Steam" doesn't also match a sibling
                // folder like "SteamVR" - only a real subdirectory/file of installDir counts.
                if (requiredDirectory is not null &&
                    !modulePath.StartsWith(requiredDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;

                var hasWindow = process.MainWindowHandle != 0;
                if (hasWindow)
                {
                    // A same-named/same-directory process with a real window is the strongest
                    // signal we have - return it immediately rather than holding out for an
                    // exact path match that may never come (see the Spotify case above).
                    return new LaunchedApp(process.Id, process.MainWindowHandle, modulePath);
                }

                var isExactPath = targetPath is not null &&
                                   string.Equals(modulePath, targetPath, StringComparison.OrdinalIgnoreCase);
                if (isExactPath)
                    exactPathWithoutWindow ??= new LaunchedApp(process.Id, 0, modulePath);
            }
        }

        return exactPathWithoutWindow;
    }

    private static async Task<nint> WaitForMainWindowAsync(Process process, CancellationToken cancellationToken)
    {
        var elapsed = 0;
        while (elapsed < WindowPollTimeoutMs)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0)
                return process.MainWindowHandle;

            await Task.Delay(WindowPollIntervalMs, cancellationToken);
            elapsed += WindowPollIntervalMs;
        }

        return 0;
    }
}
