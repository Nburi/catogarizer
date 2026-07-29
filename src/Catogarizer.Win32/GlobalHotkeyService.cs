using System.Runtime.InteropServices;
using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

/// <summary>
/// RegisterHotKey requires a window and a thread with a message loop to
/// deliver WM_HOTKEY to - runs a dedicated background thread that owns a
/// hidden message-only window (HWND_MESSAGE) just for this, so it doesn't
/// depend on WPF's own message loop/HwndSource.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0xCA70;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hwnd;
    private WndProc? _wndProc; // field, not local - must stay alive for the native callback's lifetime

    public event Action? HotkeyPressed;

    public bool Register(HotkeyModifiers modifiers, int virtualKeyCode)
    {
        Unregister();

        var ready = new ManualResetEventSlim(false);
        var success = false;

        _thread = new Thread(() => RunMessageLoop(modifiers, virtualKeyCode, ready, ok => success = ok))
        {
            IsBackground = true,
            Name = "Catogarizer-GlobalHotkey",
        };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(3));
        return success;
    }

    private void RunMessageLoop(HotkeyModifiers modifiers, int vk, ManualResetEventSlim ready, Action<bool> reportSuccess)
    {
        _threadId = GetCurrentThreadId();
        _wndProc = WndProcHandler;

        var className = "CatogarizerHotkeyWindow_" + Guid.NewGuid().ToString("N");
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = className,
        };
        RegisterClassEx(ref wc);

        _hwnd = CreateWindowEx(0, className, "CatogarizerHotkeyWindow", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            reportSuccess(false);
            ready.Set();
            return;
        }

        var nativeModifiers = ToNativeModifiers(modifiers) | MOD_NOREPEAT;
        var registered = RegisterHotKey(_hwnd, HotkeyId, nativeModifiers, (uint)vk);
        reportSuccess(registered);
        ready.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (registered) UnregisterHotKey(_hwnd, HotkeyId);
        DestroyWindow(_hwnd);
        _hwnd = IntPtr.Zero;
    }

    private IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Unregister()
    {
        if (_thread is null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose() => Unregister();

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) result |= MOD_WIN;
        return result;
    }
}
