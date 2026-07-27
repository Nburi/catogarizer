using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;

namespace Catogarizer.Win32;

public sealed class ShellIconProvider : IAppIconProvider
{
    public nint GetIconHandle(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return 0;

        var info = new ShFileInfo();
        var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<ShFileInfo>();
        var result = NativeMethods.SHGetFileInfo(executablePath, 0, ref info, size, NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
        return result == 0 ? 0 : info.hIcon;
    }

    public void ReleaseIconHandle(nint handle)
    {
        if (handle != 0)
            NativeMethods.DestroyIcon(handle);
    }
}
