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

    [Fact]
    public void Parse_SwitchWithName_ReturnsSwitchCommand()
    {
        var command = Assert.IsType<CliCommand.Switch>(CliCommand.Parse(["switch", "Deep Work"]));
        Assert.Equal("Deep Work", command.CategoryName);
    }

    [Fact]
    public void Parse_SwitchWithoutName_ReturnsNone() =>
        Assert.IsType<CliCommand.None>(CliCommand.Parse(["switch", " "]));

    [Fact]
    public void Parse_Back_ReturnsBack() =>
        Assert.IsType<CliCommand.Back>(CliCommand.Parse(["Back"]));

    [Theory]
    [InlineData("run", "Morning")]
    [InlineData("switch", "Deep Work")]
    [InlineData("back", null)]
    [InlineData("show-all", null)]
    public void PipeMessage_RoundTrips(string verb, string? name)
    {
        var original = CliCommand.Parse(name is null ? [verb] : [verb, name]);

        Assert.Equal(original, CliCommand.FromPipeMessage(original.ToPipeMessage()));
    }

    [Fact]
    public void FromPipeMessage_Garbage_ReturnsNone() =>
        Assert.IsType<CliCommand.None>(CliCommand.FromPipeMessage("delete\teverything"));
}
