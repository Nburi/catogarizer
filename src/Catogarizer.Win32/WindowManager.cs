using System.Diagnostics;
using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

public sealed class WindowManager : IWindowManager
{
    public void Position(IntPtr handle, int x, int y, int width, int height)
    {
        if (IsZoomed(handle))
        {
            ShowWindow(handle, SW_RESTORE);
        }

        var wp = new WINDOWPLACEMENT { length = System.Runtime.InteropServices.Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(handle, ref wp))
        {
            // Fallback for the rare case GetWindowPlacement itself fails.
            SetWindowPos(handle, HWND_TOP, x, y, width, height, SWP_SHOWWINDOW | SWP_NOZORDER);
            return;
        }

        wp.showCmd = SW_SHOWNORMAL;
        wp.rcNormalPosition = new RECT { Left = x, Top = y, Right = x + width, Bottom = y + height };
        SetWindowPlacement(handle, ref wp);
    }

    public (int X, int Y, int Width, int Height) GetBounds(IntPtr handle)
    {
        GetWindowRect(handle, out var rect);
        return (rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public void Minimize(IntPtr handle) => ShowWindow(handle, SW_MINIMIZE);

    public void CloseGraceful(IntPtr handle) => PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    public void ForceKill(IntPtr handle)
    {
        GetWindowThreadProcessId(handle, out var pid);
        try { Process.GetProcessById(pid).Kill(); }
        catch (ArgumentException) { /* already gone */ }
    }

    public bool IsWindowOpen(IntPtr handle) => IsWindow(handle);
}
