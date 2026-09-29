using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class DoubleTapDetectorTests
{
    private long _now = 10_000;
    private readonly DoubleTapDetector _detector;

    public DoubleTapDetectorTests()
    {
        _detector = new DoubleTapDetector(() => _now, TimeSpan.FromMilliseconds(350));
    }

    [Fact]
    public void FirstTap_IsSingle() => Assert.False(_detector.RegisterTap());

    [Fact]
    public void SecondTapWithinTheWindow_IsDouble()
    {
        _detector.RegisterTap();
        _now += 250;

        Assert.True(_detector.RegisterTap());
    }

    [Fact]
    public void SecondTapExactlyAtTheWindowEdge_IsDouble()
    {
        _detector.RegisterTap();
        _now += 350;

        Assert.True(_detector.RegisterTap());
    }

    [Fact]
    public void SecondTapAfterTheWindow_IsSingleAndStartsANewSequence()
    {
        _detector.RegisterTap();
        _now += 600;
        Assert.False(_detector.RegisterTap());

        _now += 200;
        Assert.True(_detector.RegisterTap());
    }

    [Fact]
    public void ThirdQuickTap_StartsANewSequenceInsteadOfAnotherDouble()
    {
        _detector.RegisterTap();
        _now += 150;
        Assert.True(_detector.RegisterTap());

        _now += 150;
        Assert.False(_detector.RegisterTap());
    }
}
