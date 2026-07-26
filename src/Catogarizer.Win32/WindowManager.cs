using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;

namespace Catogarizer.Win32;

public sealed class WindowManager : IWindowManager
{
    public void MoveResize(nint windowHandle, WindowRect rect)
    {
        if (!IsUsable(windowHandle))
            return;

        NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_RESTORE);
        NativeMethods.SetWindowPos(windowHandle, 0, rect.X, rect.Y, rect.Width, rect.Height, NativeMethods.SWP_NOZORDER);
    }

    public void Minimize(nint windowHandle)
    {
        if (!IsUsable(windowHandle))
            return;

        NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_MINIMIZE);
    }

    public void Restore(nint windowHandle)
    {
        if (!IsUsable(windowHandle))
            return;

        NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_RESTORE);
    }

    public void Close(nint windowHandle)
    {
        if (!IsUsable(windowHandle))
            return;

        NativeMethods.PostMessage(windowHandle, NativeMethods.WM_CLOSE, 0, 0);
    }

    private static bool IsUsable(nint windowHandle) => windowHandle != 0 && NativeMethods.IsWindow(windowHandle);
}
