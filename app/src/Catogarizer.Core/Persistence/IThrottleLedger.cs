using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Persistence;

/// <summary>
/// A process Catogarizer put to rest. <see cref="StartTicks"/> is checked together with the id on recovery,
/// so a process that reused the id after a reboot is never touched. The memento is what to put back.
/// </summary>
public sealed record ThrottleRecord(int ProcessId, long StartTicks, ResourceLevel Level, EcoMemento? Memento);

/// <summary>
/// Durable list of the processes Catogarizer currently slows down or suspends, so a crash never leaves
/// one frozen: the next start resumes them (PRINCIPLES.md value 1, same idea as <see cref="IHiddenWindowStore"/>).
/// </summary>
public interface IThrottleLedger
{
    IReadOnlyList<ThrottleRecord> Load();
    void Save(IReadOnlyList<ThrottleRecord> records);
}

public sealed class NullThrottleLedger : IThrottleLedger
{
    public IReadOnlyList<ThrottleRecord> Load() => [];
    public void Save(IReadOnlyList<ThrottleRecord> records) { }
}
