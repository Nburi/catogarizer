namespace Catogarizer.Core.Services;

public interface IWindowWatcher : IDisposable
{
    /// <summary>Raised when a new top-level, visible window appears.</summary>
    event Action<OpenWindowInfo>? WindowAppeared;

    void Start();
    void Stop();
}
