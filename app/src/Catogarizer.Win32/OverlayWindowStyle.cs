using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

public static class OverlayWindowStyle
{
    /// <summary>
    /// Turns a window into a passive overlay: clicks go through it, it never takes focus,
    /// and it stays out of Alt-Tab and the taskbar.
    /// </summary>
    public static void MakeClickThrough(IntPtr hwnd)
    {
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }
}
