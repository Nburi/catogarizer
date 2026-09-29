using Catogarizer.Core.Services;
using Catogarizer.Win32.Interop;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

public sealed class MonitorService : IMonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var results = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                var isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                results.Add(new MonitorInfo(mi.szDevice, mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height, isPrimary));
            }
            return true;
        }, IntPtr.Zero);
        return results;
    }

    public MonitorInfo GetPrimaryMonitor()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();
    }
}
