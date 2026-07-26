using System.Diagnostics;
using System.Text;
using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;

namespace Catogarizer.Win32;

public sealed class WindowEnumerator : IWindowEnumerator
{
    public IReadOnlyList<OpenWindowInfo> GetOpenWindows()
    {
        var currentProcessId = Environment.ProcessId;
        var results = new List<OpenWindowInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            // Skip windows owned by another window (dialogs/tooltips) - keep top-level app windows only.
            if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != 0)
                return true;

            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0)
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == currentProcessId)
                return true;

            var builder = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);

            results.Add(new OpenWindowInfo(hWnd, builder.ToString(), (int)processId));
            return true;
        }, 0);

        return results;
    }
}
