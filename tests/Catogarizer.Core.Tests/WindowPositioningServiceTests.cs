using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class WindowPositioningServiceTests
{
    [Fact]
    public void PositionWithRetry_WhenFirstAttemptSticks_DoesNotRetry()
    {
        var windowManager = new FakeWindowManager { AttemptsUntilItSticks = 1 };
        var delay = new FakeDelay();
        var service = new WindowPositioningService(windowManager, delay);

        var result = service.PositionWithRetry(new IntPtr(1), 10, 20, 300, 400);

        Assert.True(result);
        Assert.Equal(1, windowManager.PositionCallCount);
        Assert.Equal(1, delay.WaitCount);
    }

    [Fact]
    public void PositionWithRetry_WhenFirstAttemptIsClobbered_RetriesOnceAndSucceeds()
    {
        // Mirrors what was observed with Edge: the first positioning call gets
        // overridden by the app's own async re-layout, but a retry after a
        // short settle delay catches it.
        var windowManager = new FakeWindowManager { AttemptsUntilItSticks = 2 };
        var delay = new FakeDelay();
        var service = new WindowPositioningService(windowManager, delay);

        var result = service.PositionWithRetry(new IntPtr(1), 10, 20, 300, 400);

        Assert.True(result);
        Assert.Equal(2, windowManager.PositionCallCount);
        Assert.Equal(2, delay.WaitCount);
    }

    [Fact]
    public void PositionWithRetry_WhenAppKeepsResisting_ReturnsFalseAfterOneRetry()
    {
        // Mirrors Edge enforcing its own minimum width - an expected outcome,
        // not a bug, but the caller needs to know it didn't stick.
        var windowManager = new FakeWindowManager { AttemptsUntilItSticks = 99 };
        var delay = new FakeDelay();
        var service = new WindowPositioningService(windowManager, delay);

        var result = service.PositionWithRetry(new IntPtr(1), 10, 20, 300, 400);

        Assert.False(result);
        Assert.Equal(2, windowManager.PositionCallCount);
    }
}
