using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeWindowWatcher : IWindowWatcher
{
    public bool IsStarted { get; private set; }
    public event Action<OpenWindowInfo>? WindowAppeared;

    public void Start() => IsStarted = true;
    public void Stop() => IsStarted = false;

    /// <summary>Test hook to simulate a new window appearing.</summary>
    public void RaiseWindowAppeared(OpenWindowInfo info) => WindowAppeared?.Invoke(info);

    public void Dispose() { }
}
