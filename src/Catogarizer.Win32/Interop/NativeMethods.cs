using System.Runtime.InteropServices;
using System.Text;

namespace Catogarizer.Win32.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ShFileInfo
{
    public nint hIcon;
    public int iIcon;
    public uint dwAttributes;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szDisplayName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string szTypeName;
}

internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

internal static class NativeMethods
{
    internal const int SW_MINIMIZE = 6;
    internal const int SW_RESTORE = 9;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const int GW_OWNER = 4;
    internal const uint SHGFI_ICON = 0x000000100;
    internal const uint SHGFI_LARGEICON = 0x000000000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
