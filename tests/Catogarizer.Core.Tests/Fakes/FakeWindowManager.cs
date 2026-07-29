using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

/// <summary>
/// Simulates a window that only "sticks" to a positioning call after a
/// configurable number of attempts - the exact shape of the Edge async
/// re-layout race the retry policy exists to handle. Also tracks
/// minimize/close/kill calls and can simulate an app that ignores a
/// graceful close request (so the force-kill fallback has something to do).
/// </summary>
public sealed class FakeWindowManager : IWindowManager
{
    private (int X, int Y, int Width, int Height) _bounds = (0, 0, 0, 0);
    private int _positionCalls;
    private readonly HashSet<IntPtr> _closedWindows = new();

    public int AttemptsUntilItSticks { get; set; } = 1;
    public int PositionCallCount => _positionCalls;
    public bool GracefulCloseWorks { get; set; } = true;

    public List<IntPtr> MinimizeCalls { get; } = new();
    public List<IntPtr> CloseGracefulCalls { get; } = new();
    public List<IntPtr> ForceKillCalls { get; } = new();

    public void Position(IntPtr handle, int x, int y, int width, int height)
    {
        _positionCalls++;
        _bounds = _positionCalls >= AttemptsUntilItSticks ? (x, y, width, height) : (x + 999, y, width, height);
    }

    public (int X, int Y, int Width, int Height) GetBounds(IntPtr handle) => _bounds;

    public void Minimize(IntPtr handle) => MinimizeCalls.Add(handle);

    public void CloseGraceful(IntPtr handle)
    {
        CloseGracefulCalls.Add(handle);
        if (GracefulCloseWorks) _closedWindows.Add(handle);
    }

    public void ForceKill(IntPtr handle)
    {
        ForceKillCalls.Add(handle);
        _closedWindows.Add(handle);
    }

    public bool IsWindowOpen(IntPtr handle) => !_closedWindows.Contains(handle);
}
