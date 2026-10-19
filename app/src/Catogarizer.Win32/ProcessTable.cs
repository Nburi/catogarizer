using System.Runtime.InteropServices;
using Catogarizer.Core.Resources;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

/// <summary>Process list and counters through plain Win32 - no WMI, no performance counters, no admin.</summary>
public sealed class ProcessTable : IProcessTable
{
    // PROCESS_MEMORY_COUNTERS_EX without the two newest fields, for a Windows that rejects the full struct.
    private const uint CountersExSize = 80;

    public IReadOnlyList<ProcessNode> Snapshot()
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return [];
        try
        {
            var nodes = new List<ProcessNode>(256);
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snapshot, ref entry)) return nodes;
            do
            {
                nodes.Add(new ProcessNode((int)entry.th32ProcessID, (int)entry.th32ParentProcessID,
                    Path.GetFileNameWithoutExtension(entry.szExeFile)));
            } while (Process32NextW(snapshot, ref entry));
            return nodes;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    public ProcessUsage? Measure(int pid)
    {
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            if (!GetProcessTimes(process, out var created, out _, out var kernel, out var user)) return null;

            // Private working set: what only this process holds in RAM. The plain working set counts
            // shared pages once per process, which would add up to far too much across an Electron tree.
            long bytes;
            if (GetProcessMemoryInfo(process, out var counters, (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX2>()))
                bytes = (long)counters.PrivateWorkingSetSize;
            else if (GetProcessMemoryInfo(process, out counters, CountersExSize))
                bytes = (long)counters.PrivateUsage;
            else
                return null;

            return new ProcessUsage(bytes, TimeSpan.FromTicks(kernel + user), created);
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
