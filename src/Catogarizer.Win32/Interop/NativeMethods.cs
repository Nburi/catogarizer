using System.Runtime.InteropServices;

namespace Catogarizer.Win32.Interop;

internal static class NativeMethods
{
    internal const int SW_MINIMIZE = 6;
    internal const int SW_RESTORE = 9;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint SWP_NOZORDER = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(nint hWnd);
}
