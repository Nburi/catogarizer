using System.Runtime.InteropServices;
using Catogarizer.Core.Resources;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

public sealed class SystemMemory : ISystemMemory
{
    public int LoadPercent
    {
        get
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref status) ? (int)status.dwMemoryLoad : 0;
        }
    }
}
