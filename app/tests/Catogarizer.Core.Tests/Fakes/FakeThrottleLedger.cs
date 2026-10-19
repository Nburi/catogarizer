using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeThrottleLedger : IThrottleLedger
{
    public List<ThrottleRecord> Records { get; } = [];

    public IReadOnlyList<ThrottleRecord> Load() => Records.ToList();

    public void Save(IReadOnlyList<ThrottleRecord> records)
    {
        Records.Clear();
        Records.AddRange(records);
    }
}
