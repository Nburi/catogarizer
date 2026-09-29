using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

/// <summary>Cheap facts about running processes, without admin rights.</summary>
public static class Processes
{
    /// <summary>Works for elevated processes too (limited query rights); null if the process is gone.</summary>
    public static string? ExecutablePathOf(int processId)
    {
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            var size = 1024;
            var name = new StringBuilder(size);
            return QueryFullProcessImageName(process, 0, name, ref size) ? name.ToString() : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>The process name (exe name without ".exe"), or "?" if the process is gone.</summary>
    public static string NameOf(int processId)
    {
        // One OpenProcess instead of Process.GetProcessById, which snapshots every process on the system.
        if (ExecutablePathOf(processId) is { } path) return Path.GetFileNameWithoutExtension(path);
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return "?";
        }
    }

    /// <summary>The parent's process name (without ".exe"), or null if it has exited or can't be read.</summary>
    public static string? ParentNameOf(int processId)
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return null;
        try
        {
            // One pass over the snapshot answers both "who is my parent" and "what is its exe".
            var exeByPid = new Dictionary<uint, string>();
            uint? parentId = null;
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snapshot, ref entry)) return null;
            do
            {
                exeByPid[entry.th32ProcessID] = entry.szExeFile;
                if (entry.th32ProcessID == processId) parentId = entry.th32ParentProcessID;
            } while (Process32NextW(snapshot, ref entry));

            return parentId is { } id && exeByPid.TryGetValue(id, out var exe) ? Path.GetFileNameWithoutExtension(exe) : null;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }
}
