namespace Catogarizer.Core.Services;

public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    MonitorInfo GetPrimaryMonitor();
}
