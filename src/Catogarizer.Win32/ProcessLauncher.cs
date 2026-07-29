using System.Diagnostics;
using Catogarizer.Core.Services;

namespace Catogarizer.Win32;

public sealed class ProcessLauncher : IProcessLauncher
{
    public int Launch(string executablePath, string? arguments)
    {
        var psi = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            Arguments = arguments ?? string.Empty,
        };
        var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start \"{executablePath}\".");
        return process.Id;
    }
}
