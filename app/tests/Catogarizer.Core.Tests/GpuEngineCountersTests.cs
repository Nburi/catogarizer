using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests;

public sealed class GpuEngineCountersTests
{
    private const string Engine3D = "luid_0x00000000_0x0000F3A2_phys_0_eng_0_engtype_3D";
    private const string EngineCopy = "luid_0x00000000_0x0000F3A2_phys_0_eng_1_engtype_Copy";
    private const string OtherCard3D = "luid_0x00000000_0x0001A0B1_phys_0_eng_0_engtype_3D";

    [Fact]
    public void TryParse_SplitsTheProcessFromTheEngine()
    {
        Assert.True(GpuEngineCounters.TryParse("pid_1234_" + Engine3D, out var pid, out var engine));

        Assert.Equal(1234, pid);
        Assert.Equal(Engine3D, engine);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pid_")]
    [InlineData("pid_abc_luid_0x0_eng_0_engtype_3D")]
    [InlineData("pid_12")]
    [InlineData("pid_12_")]
    [InlineData("_Total")]
    public void TryParse_RejectsNamesThatAreNotAnEngineCounter(string instance)
    {
        Assert.False(GpuEngineCounters.TryParse(instance, out _, out _));
    }

    [Fact]
    public void Aggregate_TheCardIsAsBusyAsItsBusiestEngine_NotTheSumOfAllEngines()
    {
        var (percent, _) = GpuEngineCounters.Aggregate(
        [
            ("pid_10_" + Engine3D, 60),
            ("pid_10_" + EngineCopy, 50),
        ]);

        Assert.Equal(60, percent);
    }

    [Fact]
    public void Aggregate_ProcessesSharingAnEngineAddUp_AndAreCappedAtOneHundred()
    {
        var (percent, byProcess) = GpuEngineCounters.Aggregate(
        [
            ("pid_10_" + Engine3D, 70),
            ("pid_20_" + Engine3D, 50),
        ]);

        Assert.Equal(100, percent);
        Assert.Equal(70, byProcess[10]);
        Assert.Equal(50, byProcess[20]);
    }

    [Fact]
    public void Aggregate_EachCardIsItsOwnEngine()
    {
        var (percent, _) = GpuEngineCounters.Aggregate(
        [
            ("pid_10_" + Engine3D, 40),
            ("pid_10_" + OtherCard3D, 55),
        ]);

        Assert.Equal(55, percent);
    }

    [Fact]
    public void Aggregate_AProcessIsMeasuredByItsBusiestEngine()
    {
        var (_, byProcess) = GpuEngineCounters.Aggregate(
        [
            ("pid_10_" + Engine3D, 30),
            ("pid_10_" + EngineCopy, 45),
        ]);

        Assert.Equal(45, byProcess[10]);
    }

    [Fact]
    public void Aggregate_IgnoresIdleProcessesAndUnreadableValues()
    {
        var (percent, byProcess) = GpuEngineCounters.Aggregate(
        [
            ("pid_10_" + Engine3D, 0),
            ("pid_20_" + Engine3D, double.NaN),
            ("pid_30_" + Engine3D, -5),
            ("garbage", 80),
        ]);

        Assert.Equal(0, percent);
        Assert.Empty(byProcess);
    }

    [Fact]
    public void Aggregate_NoCounters_IsIdle()
    {
        var (percent, byProcess) = GpuEngineCounters.Aggregate([]);

        Assert.Equal(0, percent);
        Assert.Empty(byProcess);
    }
}
