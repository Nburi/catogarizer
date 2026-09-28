namespace Catogarizer.Core.Theming;

/// <summary>
/// Every theme the app offers - the same set and values as design/concepts-v2.html.
/// Fjord is the default (decided 2026-09-28, see PRINCIPLES.md).
/// </summary>
public static class ThemeCatalog
{
    public const string DefaultId = "fjord";

    public static IReadOnlyList<ThemePalette> All { get; } =
    [
        new("fjord", "Fjord", "Soft mint light, deep teal", false,
            Bg: new(0.974, 0.011, 195), Surface: new(0.996, 0.003, 195), SurfaceAlt: new(0.94, 0.016, 195),
            Ink: new(0.25, 0.035, 220), Muted: new(0.45, 0.03, 215), Border: new(0.88, 0.018, 200),
            Accent: new(0.49, 0.10, 192), AccentInk: new(0.99, 0, 0), Danger: new(0.54, 0.18, 25),
            CategoryLightness: 0.57, CategoryChroma: 0.12, CardRadius: 18, ControlRadius: 10),

        new("daylight", "Daylight Studio", "Cool light, blue", false,
            Bg: new(0.98, 0.006, 250), Surface: new(1, 0, 0), SurfaceAlt: new(0.955, 0.008, 250),
            Ink: new(0.22, 0.02, 250), Muted: new(0.45, 0.02, 250), Border: new(0.89, 0.008, 250),
            Accent: new(0.52, 0.16, 260), AccentInk: new(0.99, 0, 0), Danger: new(0.55, 0.18, 25),
            CategoryLightness: 0.58, CategoryChroma: 0.14, CardRadius: 14, ControlRadius: 8),

        new("paperink", "Paper & Ink", "Editorial, oxblood, serif", false,
            Bg: new(0.988, 0.002, 80), Surface: new(1, 0, 0), SurfaceAlt: new(0.965, 0.003, 80),
            Ink: new(0.19, 0.005, 60), Muted: new(0.42, 0.005, 60), Border: new(0.87, 0.004, 60),
            Accent: new(0.38, 0.13, 25), AccentInk: new(0.98, 0, 0), Danger: new(0.50, 0.19, 25),
            CategoryLightness: 0.52, CategoryChroma: 0.12, CardRadius: 6, ControlRadius: 4, HeadingFont: "Georgia"),

        new("mono", "High-Contrast Mono", "Accessible, hard edges", false,
            Bg: new(1, 0, 0), Surface: new(1, 0, 0), SurfaceAlt: new(0.94, 0, 0),
            Ink: new(0.10, 0, 0), Muted: new(0.33, 0, 0), Border: new(0.40, 0, 0),
            Accent: new(0.45, 0.15, 255), AccentInk: new(1, 0, 0), Danger: new(0.45, 0.20, 25),
            CategoryLightness: 0.46, CategoryChroma: 0.16, CardRadius: 4, ControlRadius: 3),

        new("graphite", "Graphite Mica", "Windows 11 dark, neutral", true,
            Bg: new(0.21, 0.006, 260), Surface: new(0.255, 0.007, 260), SurfaceAlt: new(0.29, 0.008, 260),
            Ink: new(0.96, 0.003, 260), Muted: new(0.77, 0.008, 260), Border: new(0.34, 0.008, 260),
            Accent: new(0.74, 0.12, 240), AccentInk: new(0.17, 0.03, 240), Danger: new(0.70, 0.17, 25),
            CategoryLightness: 0.74, CategoryChroma: 0.12, CardRadius: 8, ControlRadius: 6),

        new("nightshift", "Night Shift", "Warm dark, amber", true,
            Bg: new(0.19, 0.014, 55), Surface: new(0.23, 0.016, 55), SurfaceAlt: new(0.275, 0.018, 55),
            Ink: new(0.93, 0.01, 55), Muted: new(0.74, 0.02, 55), Border: new(0.33, 0.02, 55),
            Accent: new(0.76, 0.14, 68), AccentInk: new(0.18, 0.02, 65), Danger: new(0.68, 0.18, 25),
            CategoryLightness: 0.72, CategoryChroma: 0.12, CardRadius: 12, ControlRadius: 8),

        new("slate", "Slate Dusk", "Cool dark, violet", true,
            Bg: new(0.27, 0.022, 265), Surface: new(0.315, 0.024, 265), SurfaceAlt: new(0.36, 0.026, 265),
            Ink: new(0.95, 0.008, 265), Muted: new(0.79, 0.018, 265), Border: new(0.42, 0.028, 265),
            Accent: new(0.73, 0.13, 300), AccentInk: new(0.17, 0.03, 300), Danger: new(0.75, 0.16, 25),
            CategoryLightness: 0.74, CategoryChroma: 0.12, CardRadius: 12, ControlRadius: 8),
    ];

    /// <summary>Unknown or missing ids fall back to the default rather than failing startup.</summary>
    public static ThemePalette Get(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? All.First(t => t.Id == DefaultId);

    public static bool Exists(string? id) => All.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
}
