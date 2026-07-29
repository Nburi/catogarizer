using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeDelay : IDelay
{
    public int WaitCount { get; private set; }

    public void Wait(TimeSpan duration) => WaitCount++;
}
