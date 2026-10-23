namespace Catogarizer.Core.Resources;

/// <summary>
/// How busy the processor and the graphics card are at one moment, 0-100 each; -1 where the figure could not
/// be read (no graphics counters on this machine). <see cref="GpuByProcess"/> holds what each process uses of
/// the busiest graphics engine, only for processes that use any.
/// </summary>
public sealed record SystemLoadSample(int CpuPercent, int GpuPercent, IReadOnlyDictionary<int, int> GpuByProcess)
{
    /// <summary>Nothing could be read: no figure triggers anything.</summary>
    public static readonly SystemLoadSample Unreadable = new(-1, -1, new Dictionary<int, int>());
}

/// <summary>Processor and graphics load of the whole machine (what Task Manager shows as "CPU" and "GPU").</summary>
public interface ISystemLoad
{
    /// <summary>Never throws. Implementations may answer from a sample taken a moment ago.</summary>
    SystemLoadSample Sample();
}
