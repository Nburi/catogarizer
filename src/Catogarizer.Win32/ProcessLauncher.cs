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

        foreach (var process in Process.GetProcessesByName(targetName))
        {
            using (process)
            {
                try
                {
                    var modulePath = process.MainModule?.FileName;
                    if (modulePath != null &&
                        string.Equals(modulePath, entry.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new LaunchedApp(process.Id, process.MainWindowHandle, entry.ExecutablePath);
                    }
                }
                catch (Exception)
                {
                    // MainModule is inaccessible for protected/elevated processes - not a match we can use.
                }
            }
        }

        return null;
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
