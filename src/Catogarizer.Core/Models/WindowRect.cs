namespace Catogarizer.Core.Models;

public sealed class WindowRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 800;
    public int MonitorIndex { get; set; }
}
