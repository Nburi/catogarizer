using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests;

public sealed class CategoryMatcherTests
{
    [Theory]
    [InlineData("Deep Work", "", 1)]
    [InlineData("Deep Work", "de", 4)]
    [InlineData("Deep Work", "wo", 3)]
    [InlineData("Deep Work", "dw", 2)]
    [InlineData("Deep Work", "p w", 1)]
    [InlineData("Deep Work", "xyz", 0)]
    [InlineData("Comms", "COM", 4)]
    public void Score_RanksPrefixThenWordThenInitialsThenContains(string name, string query, int expected) =>
        Assert.Equal(expected, CategoryMatcher.Score(name, query));

    [Fact]
    public void Score_PrefersAWholeNamePrefixOverAWordPrefix() =>
        Assert.True(CategoryMatcher.Score("Work", "wo") > CategoryMatcher.Score("Deep Work", "wo"));
}
