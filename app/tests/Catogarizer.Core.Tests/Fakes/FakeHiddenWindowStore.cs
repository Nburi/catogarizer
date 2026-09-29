using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeHiddenWindowStore : IHiddenWindowStore
{
    public List<HiddenWindowRecord> Records { get; } = [];

    public IReadOnlyList<HiddenWindowRecord> Load() => Records.ToList();

    public void Save(IReadOnlyList<HiddenWindowRecord> records)
    {
        Records.Clear();
        Records.AddRange(records);
    }
}
