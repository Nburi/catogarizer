using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeProcessWatcher : IProcessWatcher
{
    public bool IsStarted { get; private set; }
    public event Action<RunningProcessInfo>? ProcessStarted;

    public void Start() => IsStarted = true;
    public void Stop() => IsStarted = false;

    /// <summary>Test hook to simulate a process starting.</summary>
    public void RaiseProcessStarted(RunningProcessInfo info) => ProcessStarted?.Invoke(info);

    public void Dispose() { }
}
