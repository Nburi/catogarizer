namespace Catogarizer.Core.Models;

/// <summary>
/// A saved window placement, stored relative to a monitor rather than as
/// absolute screen coordinates, so it stays correct if the monitor
/// arrangement or which monitor is primary changes. <see cref="MonitorId"/>
/// is a stable per-monitor device identifier (not a positional index);
/// if that monitor is no longer connected, callers fall back to the
/// primary monitor.
/// </summary>
public sealed class WindowRect
{
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? MonitorId { get; set; }
}
