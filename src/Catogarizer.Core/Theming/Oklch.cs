namespace Catogarizer.Core.Theming;

/// <summary>An sRGB color, 8 bits per channel.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>WCAG relative luminance, 0 (black) to 1 (white).</summary>
    public double RelativeLuminance => 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);

    /// <summary>WCAG contrast ratio, 1 to 21.</summary>
    public double ContrastWith(Rgb other)
    {
        var (a, b) = (RelativeLuminance, other.RelativeLuminance);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Linear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}

/// <summary>
/// OKLCH color: perceptual lightness (0-1), chroma, hue in degrees. The design previews in
/// design/concepts-v2.html are written in OKLCH, so the app's palettes are too - equal steps
/// in L look like equal steps, which keeps category colors evenly bright across hues.
/// </summary>
public readonly record struct Oklch(double L, double C, double H)
{
    public Rgb ToRgb()
    {
        var hueRadians = H * Math.PI / 180;
        var a = C * Math.Cos(hueRadians);
        var b = C * Math.Sin(hueRadians);

        var l = Math.Pow(L + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(L - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(L - 0.0894841775 * a - 1.2914855480 * b, 3);

        var red = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var green = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var blue = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        return new Rgb(Encode(red), Encode(green), Encode(blue));
    }

    private static byte Encode(double linear)
    {
        var clamped = Math.Clamp(linear, 0, 1);
        var gamma = clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
        return (byte)Math.Round(gamma * 255);
    }
}
