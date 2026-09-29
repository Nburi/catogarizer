namespace Catogarizer.Core.Persistence;

/// <summary>
/// A window Catogarizer hid. <see cref="ProcessId"/> is checked together with
/// the handle on recovery, so a handle reused by an unrelated window after a
/// reboot is never shown by mistake.
/// </summary>
public sealed record HiddenWindowRecord(long Handle, int ProcessId, string ProcessName, string Title);

/// <summary>
/// Durable list of windows Catogarizer currently has hidden, so a crash never
/// leaves them unreachable: the next start shows them again.
/// </summary>
public interface IHiddenWindowStore
{
    IReadOnlyList<HiddenWindowRecord> Load();
    void Save(IReadOnlyList<HiddenWindowRecord> records);
}

public sealed class NullHiddenWindowStore : IHiddenWindowStore
{
    public IReadOnlyList<HiddenWindowRecord> Load() => [];
    public void Save(IReadOnlyList<HiddenWindowRecord> records) { }
}
