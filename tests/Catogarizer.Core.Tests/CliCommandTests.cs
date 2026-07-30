using Catogarizer.Core.Cli;

namespace Catogarizer.Core.Tests;

public sealed class CliCommandTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsNone() =>
        Assert.IsType<CliCommand.None>(CliCommand.Parse([]));

    [Theory]
    [InlineData("start")]
    [InlineData("START")]
    [InlineData("Start")]
    public void Parse_Start_IsCaseInsensitive(string arg) =>
        Assert.IsType<CliCommand.Start>(CliCommand.Parse([arg]));

    [Fact]
    public void Parse_RunWithName_ReturnsRunCommandWithName()
    {
        var command = Assert.IsType<CliCommand.Run>(CliCommand.Parse(["run", "Morning"]));
        Assert.Equal("Morning", command.TriggerName);
    }

    [Fact]
    public void Parse_RunCaseInsensitiveVerb_StillParses() =>
        Assert.IsType<CliCommand.Run>(CliCommand.Parse(["RUN", "Morning"]));

    [Fact]
    public void Parse_RunWithoutName_ReturnsNone() =>
        Assert.IsType<CliCommand.None>(CliCommand.Parse(["run"]));

    [Fact]
    public void Parse_UnknownVerb_ReturnsNone() =>
        Assert.IsType<CliCommand.None>(CliCommand.Parse(["frobnicate"]));
}
