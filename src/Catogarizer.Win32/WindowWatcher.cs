using Catogarizer.Core.Services;

namespace Catogarizer.Win32;

/// <summary>
/// Polls <see cref="IWindowFinder.FindAllAppWindows"/> and diffs against
/// the previous snapshot for newly-appeared window handles - same "poll and
/// diff" shape as <see cref="ProcessWatcher"/>, applied to windows instead of
/// processes. Backs the v2 session-switching spike's "auto-attribute an
/// ad-hoc window to the active category" mechanism.
/// </summary>
public sealed class WindowWatcher : IWindowWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(350);

    private readonly IWindowFinder _finder;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private HashSet<IntPtr> _knownHandles = new();

    public event Action<OpenWindowInfo>? WindowAppeared;

    public WindowWatcher(IWindowFinder finder)
    {
        _finder = finder;
    }

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _knownHandles = _finder.FindAllAppWindows().Select(w => w.Handle).ToHashSet();
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
                var current = _finder.FindAllAppWindows();
                var currentHandles = new HashSet<IntPtr>(current.Count);
                foreach (var window in current)
                {
                    currentHandles.Add(window.Handle);
                    if (!_knownHandles.Contains(window.Handle))
                        WindowAppeared?.Invoke(window);
                }
                _knownHandles = currentHandles;
            }
            catch
            {
                // Transient enumeration hiccup (a window closed mid-scan, etc.) - just retry next tick.
            }

            try { await Task.Delay(PollInterval, token); }
            catch (TaskCanceledException) { }
        }
    }

    public void Dispose() => Stop();
}
