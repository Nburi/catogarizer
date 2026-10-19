using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeProcessTable : IProcessTable
{
    public List<ProcessNode> Nodes { get; } = [];
    public Dictionary<int, ProcessUsage> Usages { get; } = [];

    /// <summary>Adds a process. A process that starts later than its parent gets a later start time by default.</summary>
    public FakeProcessTable Add(int pid, int parent, string name, long mb = 100, long startTicks = -1, TimeSpan? cpu = null)
    {
        Nodes.Add(new ProcessNode(pid, parent, name));
        Usages[pid] = new ProcessUsage(mb * 1024 * 1024, cpu ?? TimeSpan.Zero, startTicks >= 0 ? startTicks : 1_000 + pid);
        return this;
    }

    /// <summary>A process the user cannot open (administrator): listed, but no counters.</summary>
    public FakeProcessTable AddUnreadable(int pid, int parent, string name)
    {
        Nodes.Add(new ProcessNode(pid, parent, name));
        return this;
    }

    public void SetBytes(int pid, long mb) => Usages[pid] = Usages[pid] with { PrivateBytes = mb * 1024 * 1024 };

    public void SetCpu(int pid, TimeSpan cpu) => Usages[pid] = Usages[pid] with { CpuTime = cpu };

    public IReadOnlyList<ProcessNode> Snapshot() => Nodes.ToList();

    public ProcessUsage? Measure(int pid) => Usages.TryGetValue(pid, out var usage) ? usage : null;
}
