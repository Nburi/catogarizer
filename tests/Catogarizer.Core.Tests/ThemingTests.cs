using Catogarizer.Core.Theming;

namespace Catogarizer.Core.Tests;

public sealed class ThemingTests
{
    [Theory]
    [InlineData(1.0, 0.0, 0.0, "#FFFFFF")]
    [InlineData(0.0, 0.0, 0.0, "#000000")]
    [InlineData(0.62796, 0.25768, 29.2339, "#FF0000")]
    [InlineData(0.86644, 0.29483, 142.4953, "#00FF00")]
    [InlineData(0.45201, 0.31321, 264.052, "#0000FF")]
    public void Oklch_ConvertsKnownColorsToSrgb(double l, double c, double h, string hex) =>
        Assert.Equal(hex, new Oklch(l, c, h).ToRgb().Hex);

    [Fact]
    public void Contrast_BlackOnWhiteIs21() =>
        Assert.Equal(21, new Rgb(0, 0, 0).ContrastWith(new Rgb(255, 255, 255)), precision: 2);

    public static TheoryData<string> ThemeIds() => new(ThemeCatalog.All.Select(t => t.Id));

    [Theory]
    [MemberData(nameof(ThemeIds))]
    public void EveryTheme_HasReadableText(string id)
    {
        var t = ThemeCatalog.Get(id);
        var bg = t.Bg.ToRgb();
        var surface = t.Surface.ToRgb();
        var surfaceAlt = t.SurfaceAlt.ToRgb();

        foreach (var background in new[] { bg, surface, surfaceAlt })
        {
            Assert.True(t.Ink.ToRgb().ContrastWith(background) >= 7, $"{id}: ink");
            Assert.True(t.Muted.ToRgb().ContrastWith(background) >= 4.5, $"{id}: muted on {background.Hex}");
        }
        Assert.True(t.AccentInk.ToRgb().ContrastWith(t.Accent.ToRgb()) >= 4.5, $"{id}: text on accent");
        Assert.True(t.Danger.ToRgb().ContrastWith(surface) >= 4.5, $"{id}: danger text");
        Assert.True(t.Accent.ToRgb().ContrastWith(bg) >= 3, $"{id}: accent as a UI element");
    }

    [Theory]
    [MemberData(nameof(ThemeIds))]
    public void EveryTheme_CategoryColorsStandOutFromTheBackground(string id)
    {
        var t = ThemeCatalog.Get(id);
        foreach (var hue in new[] { 25.0, 70, 150, 200, 260, 330 })
            Assert.True(t.CategoryColor(hue).ContrastWith(t.Surface.ToRgb()) >= 3, $"{id}: category hue {hue}");
    }

    [Fact]
    public void Get_UnknownId_FallsBackToFjord() =>
        Assert.Equal("fjord", ThemeCatalog.Get("does-not-exist").Id);

    [Fact]
    public void Get_IsCaseInsensitive() =>
        Assert.Equal("slate", ThemeCatalog.Get("SLATE").Id);
}
