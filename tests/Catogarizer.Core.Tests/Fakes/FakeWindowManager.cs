using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

/// <summary>
/// Simulates a window that only "sticks" to a positioning call after a
/// configurable number of attempts - the exact shape of the Edge async
/// re-layout race the retry policy exists to handle.
/// </summary>
public sealed class FakeWindowManager : IWindowManager
{
    private (int X, int Y, int Width, int Height) _bounds = (0, 0, 0, 0);
    private int _positionCalls;

    public int AttemptsUntilItSticks { get; set; } = 1;
    public int PositionCallCount => _positionCalls;

    public void Position(IntPtr handle, int x, int y, int width, int height)
    {
        _positionCalls++;
        _bounds = _positionCalls >= AttemptsUntilItSticks ? (x, y, width, height) : (x + 999, y, width, height);
    }

    public (int X, int Y, int Width, int Height) GetBounds(IntPtr handle) => _bounds;

    public void Minimize(IntPtr handle) { }
    public void CloseGraceful(IntPtr handle) { }
    public void ForceKill(IntPtr handle) { }
    public bool IsWindowOpen(IntPtr handle) => true;
}
