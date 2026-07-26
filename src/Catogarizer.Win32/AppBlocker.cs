using System.Diagnostics;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Win32;

public sealed class AppBlocker : IAppBlocker
{
    private const int PollIntervalMs = 1500;

    private readonly Lock _lock = new();
    private List<string> _blockedProcessNames = new();
    private CancellationTokenSource? _cts;

    public event EventHandler<AppBlockedEventArgs>? AppBlocked;

    public void Start(IReadOnlyList<BlockedApp> blockList)
    {
        UpdateBlockList(blockList);
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    public void UpdateBlockList(IReadOnlyList<BlockedApp> blockList)
    {
        var names = blockList
            .Select(b => Path.GetFileNameWithoutExtension(b.ProcessName))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();

        lock (_lock)
        {
            _blockedProcessNames = names;
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            List<string> namesSnapshot;
            lock (_lock)
            {
                namesSnapshot = _blockedProcessNames;
            }

            if (namesSnapshot.Count > 0)
            {
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        try
                        {
                            if (namesSnapshot.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                            {
                                var name = process.ProcessName;
                                process.Kill();
                                AppBlocked?.Invoke(this, new AppBlockedEventArgs(name, DateTimeOffset.Now));
                            }
                        }
                        catch (Exception)
                        {
                            // Protected or already-exited process - nothing we can (or should) do.
                        }
                    }
                }
            }

            try
            {
                await Task.Delay(PollIntervalMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
