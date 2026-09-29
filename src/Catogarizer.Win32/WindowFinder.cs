using System.Diagnostics;
using System.Text;
using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

public sealed class WindowFinder : IWindowFinder
{
    public IntPtr? FindMainWindow(int processId, IReadOnlyList<string> titleCandidates, TimeSpan timeout)
    {
        var byHandle = FindByProcessHandle(processId, TimeSpan.FromMilliseconds(1200));
        if (byHandle is { } h && h != IntPtr.Zero) return h;

        return FindByTitleSubstring(titleCandidates, timeout);
    }

    public IReadOnlyList<OpenWindowInfo> FindAllRunningWindows(IReadOnlyList<string> processNameCandidates, IReadOnlyList<string> titleCandidates)
    {
        var results = new List<OpenWindowInfo>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var title = ReadWindowTitle(hWnd);
            if (title.Length == 0) return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            var procName = Processes.NameOf(pid);

            var processMatches = processNameCandidates.Any(c => string.Equals(c, procName, StringComparison.OrdinalIgnoreCase));
            var titleMatches = titleCandidates.Any(c => title.Contains(c, StringComparison.OrdinalIgnoreCase));

            if (processMatches || titleMatches)
                results.Add(new OpenWindowInfo(hWnd, title, procName, pid));

            return true;
        }, IntPtr.Zero);
        return results;
    }

    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    public IReadOnlyList<OpenWindowInfo> FindAllAppWindows()
    {
        var results = new List<OpenWindowInfo>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsAltTabWindow(hWnd)) return true;
            var title = ReadWindowTitle(hWnd);
            if (title.Length == 0) return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            results.Add(new OpenWindowInfo(hWnd, title, Processes.NameOf(pid), pid));
            return true;
        }, IntPtr.Zero);
        return results;
    }

    private static bool IsAltTabWindow(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;
        if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return false;

        var exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0) return false;

        // Suspended UWP apps and windows on other virtual desktops report visible but are cloaked.
        if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return false;

        var className = new StringBuilder(64);
        GetClassName(hWnd, className, className.Capacity);
        return !ShellWindowClasses.Contains(className.ToString());
    }

    private static IntPtr? FindByProcessHandle(int processId, TimeSpan timeout)
    {
        Process process;
        try { process = Process.GetProcessById(processId); }
        catch (ArgumentException) { return null; }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
            }
            catch (InvalidOperationException)
            {
                // The launched process already exited without ever owning a window -
                // e.g. a PWA shortcut's msedge_proxy.exe/chrome_proxy.exe stub, which
                // relaunches the real browser and exits. Let the title-substring
                // fallback in FindMainWindow take over instead of surfacing this.
                return null;
            }
            Thread.Sleep(60);
        }
        return null;
    }

    private static IntPtr? FindByTitleSubstring(IReadOnlyList<string> titleCandidates, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var title = ReadWindowTitle(hWnd);
                if (title.Length == 0) return true;
                if (titleCandidates.Any(c => title.Contains(c, StringComparison.OrdinalIgnoreCase)))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero) return found;
            Thread.Sleep(80);
        }
        return null;
    }
}
