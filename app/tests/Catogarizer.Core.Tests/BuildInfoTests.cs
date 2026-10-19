using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class BuildInfoTests
{
    [Fact]
    public void From_SplitsTheSdkVersionIntoVersionAndShortCommit()
    {
        var info = BuildInfo.From("1.2.0+7418fc01d7a121ab7c1ae5c47726c29b3dd84567", isDevelopment: false);

        Assert.Equal("1.2.0", info.Version);
        Assert.Equal("7418fc0", info.Commit);
        Assert.False(info.IsDevelopment);
    }

    [Theory]
    [InlineData("1.2.0", "1.2.0", null)]
    [InlineData("1.2.0+", "1.2.0", null)]
    [InlineData("2.0.0-beta.1+abc", "2.0.0-beta.1", "abc")]
    [InlineData("  1.0.0 + abc123 ", "1.0.0", "abc123")]
    [InlineData("", "unknown", null)]
    [InlineData(null, "unknown", null)]
    [InlineData("+abc", "unknown", "abc")]
    public void From_CopesWithMissingOrOddVersions(string? raw, string version, string? commit)
    {
        var info = BuildInfo.From(raw, isDevelopment: false);

        Assert.Equal(version, info.Version);
        Assert.Equal(commit, info.Commit);
    }

    [Fact]
    public void TheInstalledApp_SaysNothingExtra_ExceptItsVersion()
    {
        var info = BuildInfo.From("1.2.0+7418fc0", isDevelopment: false);

        Assert.Equal("Version 1.2.0", info.Summary);
        Assert.Equal("Catogarizer", info.WindowTitle);
        Assert.Equal("Catogarizer · Work", info.TrayText("Work"));
        Assert.Null(info.DevChipText);
    }

    [Fact]
    public void ADevelopmentBuild_IsRecognisableEverywhere_AndNamesItsCommit()
    {
        var info = BuildInfo.From("1.0.0+414baedc8e99896f2059298aac660ad05e4fdcc8", isDevelopment: true);

        Assert.Equal("Development build 1.0.0 (414baed)", info.Summary);
        Assert.Equal("Catogarizer - development build (414baed)", info.WindowTitle);
        Assert.Equal("Catogarizer (dev) · Work", info.TrayText("Work"));
        Assert.Equal("DEV 414baed", info.DevChipText);
    }

    [Fact]
    public void ADevelopmentBuildWithoutACommit_StillSaysDev()
    {
        var info = BuildInfo.From("1.0.0", isDevelopment: true);

        Assert.Equal("Development build 1.0.0", info.Summary);
        Assert.Equal("Catogarizer - development build", info.WindowTitle);
        Assert.Equal("DEV", info.DevChipText);
    }
}
