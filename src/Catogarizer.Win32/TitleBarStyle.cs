using System.Runtime.InteropServices;
using Catogarizer.Core.Theming;

namespace Catogarizer.Win32;

/// <summary>
/// Colors a standard window's title bar to match the theme (Windows 11). On older builds
/// the attributes are ignored and the default title bar stays - nothing breaks.
/// </summary>
public static class TitleBarStyle
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(IntPtr hwnd, bool dark, Rgb caption, Rgb text, Rgb border)
    {
        if (hwnd == IntPtr.Zero) return;
        Set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, dark ? 1 : 0);
        Set(hwnd, DWMWA_CAPTION_COLOR, ColorRef(caption));
        Set(hwnd, DWMWA_TEXT_COLOR, ColorRef(text));
        Set(hwnd, DWMWA_BORDER_COLOR, ColorRef(border));
    }

    private static void Set(IntPtr hwnd, int attribute, int value) =>
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    private static int ColorRef(Rgb c) => c.R | c.G << 8 | c.B << 16;
}
