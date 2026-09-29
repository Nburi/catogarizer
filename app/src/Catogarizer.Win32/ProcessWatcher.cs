using System.Diagnostics;
using Catogarizer.Core.Services;

namespace Catogarizer.Win32;

/// <summary>
/// Polls Process.GetProcesses() and diffs against the previous snapshot for
/// newly-appeared PIDs. Not WMI Win32_ProcessStartTrace eventing - that
/// requires administrator privileges (verified: throws Access Denied when
/// subscribed from a non-elevated process), which would break the
/// no-admin-required soft-block design. The trade-off is a small
/// (~poll-interval) detection delay instead of instant notification -
/// already an accepted trade-off for a soft block.
/// </summary>
public sealed class ProcessWatcher : IProcessWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(350);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private HashSet<int> _knownPids = new();

    public event Action<RunningProcessInfo>? ProcessStarted;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _knownPids = Process.GetProcesses().Select(p => p.Id).ToHashSet();
        _loopTask = Task.Run(() => PollLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var current = Process.GetProcesses();
                var currentPids = new HashSet<int>(current.Length);
                foreach (var process in current)
                {
                    currentPids.Add(process.Id);
                    if (!_knownPids.Contains(process.Id))
                        ProcessStarted?.Invoke(new RunningProcessInfo(process.Id, process.ProcessName, TryGetPath(process)));
                }
                _knownPids = currentPids;
            }
            catch
            {
                // Transient enumeration hiccup (a process exited mid-scan, etc.) - just retry next tick.
            }

            try { await Task.Delay(PollInterval, token); }
            catch (TaskCanceledException) { }
        }
    }

    /// <summary>
    /// MainModule access can throw for processes we don't have query rights to
    /// (elevated/protected/different-bitness processes) - degrade to name-only
    /// matching for those rather than losing the whole detection.
    /// </summary>
    private static string? TryGetPath(Process process)
    {
        try { return process.MainModule?.FileName; }
        catch { return null; }
    }

    public void Dispose() => Stop();
}
