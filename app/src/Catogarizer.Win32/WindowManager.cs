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
        // GetWindowRect reports an off-screen sentinel rect (-32000,-32000, tiny
        // size) for a currently-minimized window. GetWindowPlacement's
        // rcNormalPosition is defined to hold the real restore-to bounds
        // regardless of current show state - matches what Position() already
        // writes into, so this stays symmetric with the write path below.
        var wp = new WINDOWPLACEMENT { length = System.Runtime.InteropServices.Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (GetWindowPlacement(handle, ref wp))
        {
            var r = wp.rcNormalPosition;
            return (r.Left, r.Top, r.Width, r.Height);
        }

        GetWindowRect(handle, out var rect);
        return (rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public void Minimize(IntPtr handle) => ShowWindow(handle, SW_MINIMIZE);

    public void Restore(IntPtr handle) => ShowWindow(handle, SW_RESTORE);

    public void CloseGraceful(IntPtr handle) => PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    public ForceKillOutcome ForceKill(IntPtr handle)
    {
        if (GetWindowThreadProcessId(handle, out var pid) == 0 || pid == 0) return ForceKillOutcome.AlreadyGone;
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            // Kill only requests the end; wait so the windows are really gone when the caller looks.
            process.WaitForExit(2000);
            return ForceKillOutcome.Ended;
        }
        catch (ArgumentException) { return ForceKillOutcome.AlreadyGone; }
        catch (System.ComponentModel.Win32Exception) { return ForceKillOutcome.AccessDenied; }
        catch (InvalidOperationException) { return ForceKillOutcome.AlreadyGone; }
        catch (NotSupportedException) { return ForceKillOutcome.Failed; }
    }

    public bool HasOpenDialog(IntPtr handle)
    {
        if (!IsWindow(handle)) return false;
        var found = false;
        EnumWindows((candidate, _) =>
        {
            if (candidate != handle && GetWindow(candidate, GW_OWNER) == handle && NativeMethods.IsWindowVisible(candidate))
                found = true;
            return !found;
        }, IntPtr.Zero);
        return found;
    }

    public bool IsWindowOpen(IntPtr handle) => IsWindow(handle);

    public void Hide(IntPtr handle) => ShowWindow(handle, SW_HIDE);

    public void Show(IntPtr handle) => ShowWindow(handle, SW_SHOW);

    public bool IsWindowVisible(IntPtr handle) => NativeMethods.IsWindowVisible(handle);

    public string GetTitle(IntPtr handle) => ReadWindowTitle(handle);

    public bool IsMinimized(IntPtr handle) => IsIconic(handle);

    public void BringToFront(IntPtr handle)
    {
        if (IsIconic(handle)) ShowWindow(handle, SW_RESTORE);
        if (SetForegroundWindow(handle) && GetForegroundWindow() == handle) return;

        // Windows refuses the call when Catogarizer did not receive the last input (a hotkey from a
        // background process, a CLI/trigger switch) and only flashes the taskbar button. Sharing the
        // input queue of the current foreground thread lifts that restriction - no fake keystroke needed.
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0u : (uint)GetWindowThreadProcessId(foreground, out _);
        var ownThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ownThread
                       && AttachThreadInput(ownThread, foregroundThread, true);
        try
        {
            SetForegroundWindow(handle);
        }
        finally
        {
            if (attached) AttachThreadInput(ownThread, foregroundThread, false);
        }
    }

    public int GetProcessId(IntPtr handle)
    {
        if (!IsWindow(handle)) return 0;
        GetWindowThreadProcessId(handle, out var pid);
        return pid;
    }
}
