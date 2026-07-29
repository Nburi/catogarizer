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
}
