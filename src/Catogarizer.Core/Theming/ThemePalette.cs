namespace Catogarizer.Core.Theming;

/// <param name="CategoryLightness">OKLCH lightness for category colors, tuned so every hue reads on this theme.</param>
/// <param name="CardRadius">Corner radius of cards and panels, in DIPs.</param>
/// <param name="ControlRadius">Corner radius of buttons and inputs, in DIPs.</param>
/// <param name="HeadingFont">Font family for large headings; null uses the UI font.</param>
public sealed record ThemePalette(
    string Id,
    string Name,
    string Description,
    bool IsDark,
    Oklch Bg,
    Oklch Surface,
    Oklch SurfaceAlt,
    Oklch Ink,
    Oklch Muted,
    Oklch Border,
    Oklch Accent,
    Oklch AccentInk,
    Oklch Danger,
    double CategoryLightness,
    double CategoryChroma,
    double CardRadius,
    double ControlRadius,
    string? HeadingFont = null)
{
    public Rgb CategoryColor(double hue) => new Oklch(CategoryLightness, CategoryChroma, hue).ToRgb();
}
