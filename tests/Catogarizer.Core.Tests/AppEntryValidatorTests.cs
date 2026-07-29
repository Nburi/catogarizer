using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class AppEntryValidatorTests
{
    [Fact]
    public void ValidateName_RejectsEmpty()
    {
        Assert.False(AppEntryValidator.ValidateName("").IsValid);
    }

    [Fact]
    public void ValidateName_AcceptsNonEmpty()
    {
        Assert.True(AppEntryValidator.ValidateName("VS Code").IsValid);
    }

    [Fact]
    public void ValidateExecutablePath_RejectsEmpty()
    {
        var result = AppEntryValidator.ValidateExecutablePath("", _ => true);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateExecutablePath_RejectsNonExeExtension()
    {
        var result = AppEntryValidator.ValidateExecutablePath(@"C:\some\file.txt", _ => true);

        Assert.False(result.IsValid);
        Assert.Contains(".exe", result.ErrorMessage);
    }

    [Fact]
    public void ValidateExecutablePath_RejectsPathThatDoesNotExist()
    {
        var result = AppEntryValidator.ValidateExecutablePath(@"C:\nope\app.exe", _ => false);

        Assert.False(result.IsValid);
        Assert.Contains("doesn't exist", result.ErrorMessage);
    }

    [Fact]
    public void ValidateExecutablePath_AcceptsExistingExe()
    {
        var result = AppEntryValidator.ValidateExecutablePath(@"C:\app\code.exe", _ => true);

        Assert.True(result.IsValid);
    }
}
