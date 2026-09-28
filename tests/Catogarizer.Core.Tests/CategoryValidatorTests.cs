using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class CategoryValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateName_RejectsEmptyOrWhitespace(string name)
    {
        var result = CategoryValidator.ValidateName(name, []);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("Unsorted")]
    [InlineData(" unsorted ")]
    public void ValidateName_RejectsTheBuiltInUnsortedName(string name)
    {
        var result = CategoryValidator.ValidateName(name, []);

        Assert.False(result.IsValid);
        Assert.Contains("built in", result.ErrorMessage);
    }

    [Fact]
    public void ValidateName_RejectsTooLong()
    {
        var result = CategoryValidator.ValidateName(new string('a', 61), []);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateName_RejectsCaseInsensitiveDuplicate()
    {
        var existing = new[] { new Category { Name = "Deep Work" } };

        var result = CategoryValidator.ValidateName("deep work", existing);

        Assert.False(result.IsValid);
        Assert.Contains("already exists", result.ErrorMessage);
    }

    [Fact]
    public void ValidateName_AllowsRenamingCategoryToItsOwnCurrentName()
    {
        var category = new Category { Name = "Deep Work" };

        var result = CategoryValidator.ValidateName("Deep Work", [category], excludingId: category.Id);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateName_AcceptsValidUniqueName()
    {
        var result = CategoryValidator.ValidateName("Design", [new Category { Name = "Deep Work" }]);

        Assert.True(result.IsValid);
    }
}
