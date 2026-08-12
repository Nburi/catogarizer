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
            var title = GetTitle(hWnd);
            if (title.Length == 0) return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            var procName = TryGetProcessName(pid);

            var processMatches = processNameCandidates.Any(c => string.Equals(c, procName, StringComparison.OrdinalIgnoreCase));
            var titleMatches = titleCandidates.Any(c => title.Contains(c, StringComparison.OrdinalIgnoreCase));

            if (processMatches || titleMatches)
                results.Add(new OpenWindowInfo(hWnd, title, procName, pid));

            return true;
        }, IntPtr.Zero);
        return results;
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
                var title = GetTitle(hWnd);
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

    private static string GetTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string TryGetProcessName(int pid)
    {
        try { return Process.GetProcessById(pid).ProcessName; }
        catch (ArgumentException) { return "?"; }
    }
}
