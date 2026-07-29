namespace Catogarizer.Core.Services;

/// <summary>
/// Single-attempt Win32 window operations. Deliberately no retry policy in
/// here - that's <see cref="WindowPositioningService"/>'s job, kept separate
/// so the retry logic (the hard-won part) is unit-testable without a real
/// desktop, while this interface stays a thin wrapper over the OS calls.
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Positions a window at the given absolute screen coordinates. Restores
    /// the window first if it's currently maximized/full-screen - positioning
    /// a still-maximized window is unreliable.
    /// </summary>
    void Position(IntPtr handle, int x, int y, int width, int height);

    (int X, int Y, int Width, int Height) GetBounds(IntPtr handle);

    void Minimize(IntPtr handle);

    /// <summary>Sends WM_CLOSE - the same message a title-bar X button sends.</summary>
    void CloseGraceful(IntPtr handle);

    void ForceKill(IntPtr handle);

    bool IsWindowOpen(IntPtr handle);
}
