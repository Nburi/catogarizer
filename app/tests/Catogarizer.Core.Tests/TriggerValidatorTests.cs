using Catogarizer.Core.Automation;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class TriggerValidatorTests
{
    [Fact]
    public void ValidateName_Empty_Fails() =>
        Assert.False(TriggerValidator.ValidateName("", []).IsValid);

    [Fact]
    public void ValidateName_Duplicate_Fails()
    {
        var existing = new[] { new Trigger { Name = "Morning" } };

        Assert.False(TriggerValidator.ValidateName("morning", existing).IsValid);
    }

    [Fact]
    public void ValidateName_DuplicateExcludingSelf_Succeeds()
    {
        var trigger = new Trigger { Name = "Morning" };

        Assert.True(TriggerValidator.ValidateName("Morning", [trigger], excludingId: trigger.Id).IsValid);
    }

    [Fact]
    public void ValidateTimeOfDay_NonTimeType_AlwaysValid() =>
        Assert.True(TriggerValidator.ValidateTimeOfDay(TriggerType.Manual, null).IsValid);

    [Fact]
    public void ValidateTimeOfDay_TimeTypeWithoutValue_Fails() =>
        Assert.False(TriggerValidator.ValidateTimeOfDay(TriggerType.Time, null).IsValid);

    [Theory]
    [InlineData("8:00")]
    [InlineData("25:00")]
    [InlineData("not a time")]
    public void ValidateTimeOfDay_BadFormat_Fails(string value) =>
        Assert.False(TriggerValidator.ValidateTimeOfDay(TriggerType.Time, value).IsValid);

    [Fact]
    public void ValidateTimeOfDay_GoodFormat_Succeeds() =>
        Assert.True(TriggerValidator.ValidateTimeOfDay(TriggerType.Time, "08:00").IsValid);
}
