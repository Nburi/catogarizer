using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// Resolves a saved <see cref="WindowRect"/> (monitor-relative) against the
/// monitors actually connected right now, falling back to the primary
/// monitor if the one it was saved against is no longer there.
/// </summary>
public static class WindowPlacementResolver
{
    public static MonitorInfo ResolveMonitor(WindowRect placement, IReadOnlyList<MonitorInfo> monitors, MonitorInfo primary)
    {
        if (placement.MonitorId is not null)
        {
            var match = monitors.FirstOrDefault(m => m.Id == placement.MonitorId);
            if (match is not null) return match;
        }
        return primary;
    }

    public static (int X, int Y, int Width, int Height) ResolveAbsoluteRect(WindowRect placement, MonitorInfo monitor) =>
        (monitor.X + placement.OffsetX, monitor.Y + placement.OffsetY, placement.Width, placement.Height);

    /// <summary>
    /// The reverse direction: given a window's current absolute bounds, find which
    /// connected monitor it's on (by its center point) and build the monitor-relative
    /// placement to save - the core of the "grab, don't type" capture flow.
    /// </summary>
    public static WindowRect CaptureFromBounds((int X, int Y, int Width, int Height) bounds, IReadOnlyList<MonitorInfo> monitors, MonitorInfo primary)
    {
        var centerX = bounds.X + bounds.Width / 2;
        var centerY = bounds.Y + bounds.Height / 2;
        var monitor = monitors.FirstOrDefault(m => Contains(m, centerX, centerY)) ?? primary;

        return new WindowRect
        {
            OffsetX = bounds.X - monitor.X,
            OffsetY = bounds.Y - monitor.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            MonitorId = monitor.Id,
        };
    }

    private static bool Contains(MonitorInfo m, int x, int y) =>
        x >= m.X && x < m.X + m.Width && y >= m.Y && y < m.Y + m.Height;
}
