using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;

namespace Catogarizer.Win32;

public sealed class WindowManager : IWindowManager
{
    private const int ConfirmTimeoutMs = 2000;
    private const int ConfirmPollIntervalMs = 100;

    public void MoveResize(nint windowHandle, WindowRect rect)
    {
        if (!IsUsable(windowHandle))
            return;

        NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_RESTORE);
        NativeMethods.SetWindowPos(windowHandle, 0, rect.X, rect.Y, rect.Width, rect.Height, NativeMethods.SWP_NOZORDER);
    }

    public async Task<bool> MinimizeAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        if (!IsUsable(windowHandle))
            return false;

        NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_MINIMIZE);

        // ShowWindow returns as soon as the request is posted, not once the window has actually
        // become iconic - some apps ignore/delay it, so poll for the real end state instead of
        // trusting the call succeeded.
        return await PollUntilAsync(() => NativeMethods.IsIconic(windowHandle), cancellationToken);
    }

    public void Restore(nint windowHandle)
    {
        if (!IsUsable(windowHandle))
            return;

        NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_RESTORE);
    }

    public async Task<bool> CloseAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        if (!IsUsable(windowHandle))
            return true;

        NativeMethods.PostMessage(windowHandle, NativeMethods.WM_CLOSE, 0, 0);

        // PostMessage only enqueues WM_CLOSE and returns immediately - the target window may
        // ignore it, prompt to save, or hide to tray instead of closing. Poll until the window
        // handle is actually destroyed instead of assuming the post succeeded.
        return await PollUntilAsync(() => !NativeMethods.IsWindow(windowHandle), cancellationToken);
    }

    private static async Task<bool> PollUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var elapsed = 0;
        while (elapsed < ConfirmTimeoutMs)
        {
            if (condition())
                return true;

            await Task.Delay(ConfirmPollIntervalMs, cancellationToken);
            elapsed += ConfirmPollIntervalMs;
        }

        return condition();
    }

    public WindowRect? GetRect(nint windowHandle)
    {
        if (!IsUsable(windowHandle))
            return null;

        if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
            return null;

        return new WindowRect
        {
            X = rect.Left,
            Y = rect.Top,
            Width = rect.Right - rect.Left,
            Height = rect.Bottom - rect.Top,
        };
    }

    private static bool IsUsable(nint windowHandle) => windowHandle != 0 && NativeMethods.IsWindow(windowHandle);
}
