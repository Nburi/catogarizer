using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeSystemMemory : ISystemMemory
{
    public int LoadPercent { get; set; } = 40;
}
