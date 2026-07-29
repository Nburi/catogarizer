using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class WindowPlacementResolverTests
{
    private static readonly MonitorInfo Primary = new("\\\\.\\DISPLAY1", 0, 0, 1920, 1080, IsPrimary: true);
    private static readonly MonitorInfo Secondary = new("\\\\.\\DISPLAY2", 1920, 0, 2560, 1440, IsPrimary: false);

    [Fact]
    public void ResolveMonitor_WhenSavedMonitorIsConnected_ReturnsIt()
    {
        var placement = new WindowRect { MonitorId = Secondary.Id };

        var resolved = WindowPlacementResolver.ResolveMonitor(placement, [Primary, Secondary], Primary);

        Assert.Equal(Secondary, resolved);
    }

    [Fact]
    public void ResolveMonitor_WhenSavedMonitorIsNoLongerConnected_FallsBackToPrimary()
    {
        var placement = new WindowRect { MonitorId = "\\\\.\\DISPLAY_UNPLUGGED" };

        var resolved = WindowPlacementResolver.ResolveMonitor(placement, [Primary], Primary);

        Assert.Equal(Primary, resolved);
    }

    [Fact]
    public void ResolveMonitor_WhenPlacementHasNoMonitorId_FallsBackToPrimary()
    {
        var placement = new WindowRect { MonitorId = null };

        var resolved = WindowPlacementResolver.ResolveMonitor(placement, [Primary, Secondary], Primary);

        Assert.Equal(Primary, resolved);
    }

    [Fact]
    public void ResolveAbsoluteRect_OffsetsFromTheMonitorsOrigin()
    {
        var placement = new WindowRect { OffsetX = 50, OffsetY = 60, Width = 800, Height = 600 };

        var (x, y, width, height) = WindowPlacementResolver.ResolveAbsoluteRect(placement, Secondary);

        Assert.Equal(1970, x); // Secondary.X (1920) + OffsetX (50)
        Assert.Equal(60, y);
        Assert.Equal(800, width);
        Assert.Equal(600, height);
    }
}
