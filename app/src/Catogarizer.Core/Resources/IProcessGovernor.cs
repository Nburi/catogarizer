namespace Catogarizer.Core.Resources;

/// <summary>
/// What a process looked like before it entered Eco, so leaving puts it back exactly there and not
/// to a guess (an updater may have been running at below-normal on purpose).
/// </summary>
public readonly record struct EcoMemento(int PriorityClass, uint ThrottleControlMask, uint ThrottleStateMask);

/// <summary>
/// Single-attempt process operations on other processes of the same user (no admin, no retries - the
/// policy lives in <see cref="ResourceGovernor"/>). Every call answers "did it work"; a process that is
/// gone, elevated or protected simply answers no and is skipped.
/// </summary>
public interface IProcessGovernor
{
    /// <summary>Efficiency mode and low priority. Null if the process could not be changed.</summary>
    EcoMemento? EnterEco(int pid);

    /// <summary>Puts the process back as the memento describes.</summary>
    bool LeaveEco(int pid, EcoMemento memento);

    /// <summary>Pages the working set out. Does not change what the process may use afterwards.</summary>
    bool Trim(int pid);

    bool Suspend(int pid);

    bool Resume(int pid);
}
