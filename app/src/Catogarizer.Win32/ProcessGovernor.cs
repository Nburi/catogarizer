using System.Runtime.InteropServices;
using Catogarizer.Core.Resources;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

/// <summary>
/// Efficiency mode, trim, suspend and resume on other processes of the same user. No admin rights:
/// a process that can't be opened (elevated, protected, gone) answers "no" and is left alone.
/// </summary>
public sealed class ProcessGovernor : IProcessGovernor
{
    private static readonly uint ThrottleSize = (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>();

    public EcoMemento? EnterEco(int pid)
    {
        var process = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var priority = GetPriorityClass(process);
            if (priority == 0) return null;

            var current = new PROCESS_POWER_THROTTLING_STATE { Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION };
            if (!GetProcessInformation(process, ProcessPowerThrottling, ref current, ThrottleSize))
                current = new PROCESS_POWER_THROTTLING_STATE { Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION };

            var eco = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            };
            var throttled = SetProcessInformation(process, ProcessPowerThrottling, ref eco, ThrottleSize);
            var lowered = SetPriorityClass(process, IDLE_PRIORITY_CLASS);
            return throttled || lowered ? new EcoMemento((int)priority, current.ControlMask, current.StateMask) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public bool LeaveEco(int pid, EcoMemento memento)
    {
        var process = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return false;
        try
        {
            var back = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = memento.ThrottleControlMask,
                StateMask = memento.ThrottleStateMask,
            };
            var throttled = SetProcessInformation(process, ProcessPowerThrottling, ref back, ThrottleSize);
            var priority = SetPriorityClass(process, (uint)memento.PriorityClass);
            return throttled && priority;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public bool Trim(int pid)
    {
        var process = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return false;
        try
        {
            return EmptyWorkingSet(process);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public bool Suspend(int pid) => WithSuspendRights(pid, NtSuspendProcess);

    public bool Resume(int pid) => WithSuspendRights(pid, NtResumeProcess);

    private static bool WithSuspendRights(int pid, Func<IntPtr, int> operation)
    {
        var process = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (process == IntPtr.Zero) return false;
        try
        {
            return operation(process) >= 0;
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
