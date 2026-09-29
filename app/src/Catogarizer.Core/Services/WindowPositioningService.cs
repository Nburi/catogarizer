namespace Catogarizer.Core.Services;

/// <summary>
/// The settle-and-retry policy found necessary while prototyping: some apps
/// (Edge/Chromium observed) asynchronously re-apply their own remembered
/// window state shortly after launch, clobbering an early positioning call.
/// One retry after a short wait catches that; if it still doesn't match,
/// the app is actively resisting (e.g. Edge enforcing its own minimum
/// width) and that's an expected outcome, not necessarily a bug.
/// </summary>
public sealed class WindowPositioningService
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(300);

    private readonly IWindowManager _windowManager;
    private readonly IDelay _delay;

    public WindowPositioningService(IWindowManager windowManager, IDelay delay)
    {
        _windowManager = windowManager;
        _delay = delay;
    }

    /// <returns>true if the window's bounds matched the target after positioning (first or retried attempt).</returns>
    public bool PositionWithRetry(IntPtr handle, int x, int y, int width, int height)
    {
        _windowManager.Position(handle, x, y, width, height);
        _delay.Wait(SettleDelay);
        if (Matches(handle, x, y, width, height)) return true;

        _windowManager.Position(handle, x, y, width, height);
        _delay.Wait(SettleDelay);
        return Matches(handle, x, y, width, height);
    }

    private bool Matches(IntPtr handle, int x, int y, int width, int height)
    {
        var bounds = _windowManager.GetBounds(handle);
        return bounds.X == x && bounds.Y == y && bounds.Width == width && bounds.Height == height;
    }
}
