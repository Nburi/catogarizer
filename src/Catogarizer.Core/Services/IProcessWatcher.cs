namespace Catogarizer.Core.Services;

public sealed record RunningProcessInfo(int ProcessId, string ProcessName, string? ExecutablePath);

public interface IProcessWatcher : IDisposable
{
    event Action<RunningProcessInfo>? ProcessStarted;

    void Start();
    void Stop();
}
