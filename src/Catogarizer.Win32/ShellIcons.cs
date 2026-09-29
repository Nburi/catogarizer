using System.Runtime.InteropServices;

namespace Catogarizer.Win32;

/// <summary>
/// Icon handles for executables. Handles returned
/// by <see cref="Extract"/> must be released with <see cref="Destroy"/>.
/// </summary>
public static class ShellIcons
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[]? ids, uint count, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    /// <summary>The exe's own icon at <paramref name="sizePx"/>, or the shell's icon for the file; zero if neither exists.</summary>
    public static IntPtr Extract(string path, int sizePx)
    {
        if (!File.Exists(path)) return IntPtr.Zero;

        var icons = new IntPtr[1];
        if (PrivateExtractIcons(path, 0, sizePx, sizePx, icons, null, 1, 0) == 1 && icons[0] != IntPtr.Zero)
            return icons[0];

        var info = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
        return info.hIcon;
    }

    public static void Destroy(IntPtr icon)
    {
        if (icon != IntPtr.Zero) DestroyIcon(icon);
    }
}
