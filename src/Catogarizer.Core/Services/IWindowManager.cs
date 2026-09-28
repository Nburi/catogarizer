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

    /// <summary>
    /// Restores a minimized (or maximized) window to normal. A minimized
    /// window's reported bounds are Windows' off-screen sentinel rect, not
    /// its real position - callers that read bounds (the placement-capture
    /// flow) need this first or they capture garbage.
    /// </summary>
    void Restore(IntPtr handle);

    /// <summary>Sends WM_CLOSE - the same message a title-bar X button sends.</summary>
    void CloseGraceful(IntPtr handle);

    void ForceKill(IntPtr handle);

    bool IsWindowOpen(IntPtr handle);

    /// <summary>
    /// Hides a window without closing it (SW_HIDE) - the primitive behind
    /// category switching's "park this session" behavior. Unlike minimize,
    /// a hidden window doesn't appear in Alt-Tab/taskbar at all.
    /// </summary>
    void Hide(IntPtr handle);

    /// <summary>
    /// Re-shows a window hidden via <see cref="Hide"/>, in its current size/
    /// position/state (SW_SHOW) - doesn't force it to normal/restored the
    /// way <see cref="Restore"/> does, so a window that was maximized before
    /// being hidden comes back maximized.
    /// </summary>
    void Show(IntPtr handle);

    bool IsWindowVisible(IntPtr handle);

    /// <summary>Current title, or an empty string if the window is gone.</summary>
    string GetTitle(IntPtr handle);

    /// <summary>Owning process id, or 0 if the window is gone.</summary>
    int GetProcessId(IntPtr handle);
}
