namespace Catogarizer.Core.Resources;

/// <summary>One running process as the process list knows it (no handle needed, so it is cheap).</summary>
public sealed record ProcessNode(int Pid, int ParentPid, string Name);

/// <summary>
/// Counters of one process. <see cref="StartTicks"/> (the creation time) tells a process from a later one
/// that got the same id - the ledger never acts on a stranger.
/// </summary>
public readonly record struct ProcessUsage(long PrivateBytes, TimeSpan CpuTime, long StartTicks);

/// <summary>Read-only facts about running processes, without admin rights.</summary>
public interface IProcessTable
{
    /// <summary>Every running process with its parent. Parents can be gone or reused - callers must cope.</summary>
    IReadOnlyList<ProcessNode> Snapshot();

    /// <summary>Null if the process is gone or cannot be opened (elevated, other session).</summary>
    ProcessUsage? Measure(int pid);
}
