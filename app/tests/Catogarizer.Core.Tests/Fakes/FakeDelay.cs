using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeDelay : IDelay
{
    public int WaitCount { get; private set; }

    /// <summary>Runs on every wait - what happens "meanwhile" (a user answering a Save prompt).</summary>
    public Action? OnWait { get; set; }

    public void Wait(TimeSpan duration)
    {
        WaitCount++;
        OnWait?.Invoke();
    }
}
