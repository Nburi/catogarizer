using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeProcessGovernor : IProcessGovernor
{
    public static readonly EcoMemento Original = new(32, 0, 0);

    public HashSet<int> Eco { get; } = [];
    public HashSet<int> Suspended { get; } = [];
    public List<int> TrimCalls { get; } = [];
    public List<int> EnterEcoCalls { get; } = [];
    public List<int> SuspendCalls { get; } = [];
    public List<int> ResumeCalls { get; } = [];
    public List<(int Pid, EcoMemento Memento)> LeaveEcoCalls { get; } = [];

    /// <summary>Processes that refuse every change, like an elevated one.</summary>
    public HashSet<int> Unreachable { get; } = [];

    public Action<int>? OnTrim { get; set; }
    public Action<int>? OnEnterEco { get; set; }
    public Action<int>? OnSuspend { get; set; }

    public EcoMemento? EnterEco(int pid)
    {
        EnterEcoCalls.Add(pid);
        OnEnterEco?.Invoke(pid);
        if (Unreachable.Contains(pid)) return null;
        Eco.Add(pid);
        return Original;
    }

    public bool LeaveEco(int pid, EcoMemento memento)
    {
        LeaveEcoCalls.Add((pid, memento));
        return Eco.Remove(pid);
    }

    public bool Trim(int pid)
    {
        TrimCalls.Add(pid);
        OnTrim?.Invoke(pid);
        return !Unreachable.Contains(pid);
    }

    public bool Suspend(int pid)
    {
        SuspendCalls.Add(pid);
        OnSuspend?.Invoke(pid);
        if (Unreachable.Contains(pid)) return false;
        Suspended.Add(pid);
        return true;
    }

    public bool Resume(int pid)
    {
        ResumeCalls.Add(pid);
        return Suspended.Remove(pid);
    }
}
