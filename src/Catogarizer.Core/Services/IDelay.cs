namespace Catogarizer.Core.Services;

/// <summary>Abstracts waiting so retry policies can be unit-tested without real sleeps.</summary>
public interface IDelay
{
    void Wait(TimeSpan duration);
}

public sealed class SystemDelay : IDelay
{
    public void Wait(TimeSpan duration) => Thread.Sleep(duration);
}
