using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeSystemLoad : ISystemLoad
{
    public int CpuPercent { get; set; }
    public int GpuPercent { get; set; }
    public Dictionary<int, int> GpuByProcess { get; } = [];

    /// <summary>How often the governor asked: it must not look when nobody freezes under load.</summary>
    public int Samples { get; private set; }

    public SystemLoadSample Sample()
    {
        Samples++;
        return new SystemLoadSample(CpuPercent, GpuPercent, new Dictionary<int, int>(GpuByProcess));
    }
}
