using Catogarizer.Core;
using Catogarizer.Core.Models;
using Xunit;

namespace Catogarizer.Core.Tests;

public class ValidationTests
{
    [Fact]
    public void ValidateAppEntry_ReturnsNoErrors_ForValidEntry()
    {
        var entry = new AppEntry
        {
            Name = "Editor",
            ExecutablePath = @"C:\Apps\editor.exe",
            Window = new WindowRect { Width = 1280, Height = 800 },
        };

        var errors = Validation.ValidateAppEntry(entry);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", "editor.exe", 1280, 800, 0)]
    [InlineData("Editor", "", 1280, 800, 0)]
    [InlineData("Editor", "editor.exe", 0, 800, 0)]
    [InlineData("Editor", "editor.exe", 1280, 0, 0)]
    [InlineData("Editor", "editor.exe", 1280, 800, -1)]
    public void ValidateAppEntry_ReturnsError_ForInvalidField(
        string name, string exePath, int width, int height, int delay)
    {
        var entry = new AppEntry
        {
            Name = name,
            ExecutablePath = exePath,
            Window = new WindowRect { Width = width, Height = height },
            LaunchDelayMs = delay,
        };

        var errors = Validation.ValidateAppEntry(entry);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateCategoryName_ReturnsError_WhenEmpty()
    {
        var errors = Validation.ValidateCategoryName("   ", Enumerable.Empty<Category>());

        Assert.Contains(errors, e => e.Contains("required"));
    }

    [Fact]
    public void ValidateCategoryName_ReturnsError_WhenDuplicate()
    {
        var existing = new[] { new Category { Name = "Deep Work" } };

        var errors = Validation.ValidateCategoryName("deep work", existing);

        Assert.Contains(errors, e => e.Contains("already exists"));
    }

    [Fact]
    public void ValidateCategoryName_AllowsSameName_WhenEditingThatSameCategory()
    {
        var category = new Category { Name = "Deep Work" };
        var existing = new[] { category };

        var errors = Validation.ValidateCategoryName("Deep Work", existing, editingId: category.Id);

        Assert.Empty(errors);
    }
}
