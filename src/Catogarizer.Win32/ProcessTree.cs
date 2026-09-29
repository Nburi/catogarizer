using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Catogarizer.Win32;

/// <summary>Parent-process lookup via a Toolhelp snapshot (no admin rights needed).</summary>
public static class ProcessTree
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>The parent's process name (without .exe), or null if it has exited or can't be read.</summary>
    public static string? GetParentProcessName(int processId)
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandle) return null;
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snapshot, ref entry)) return null;
            do
            {
                if (entry.th32ProcessID != processId) continue;
                try
                {
                    using var parent = Process.GetProcessById((int)entry.th32ParentProcessID);
                    return parent.ProcessName;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    return null; // parent already gone
                }
            } while (Process32NextW(snapshot, ref entry));
            return null;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }
}
