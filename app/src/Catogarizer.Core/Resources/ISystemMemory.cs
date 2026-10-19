namespace Catogarizer.Core.Resources;

public interface ISystemMemory
{
    /// <summary>Physical memory in use, 0-100 (what Task Manager shows as "Memory").</summary>
    int LoadPercent { get; }
}
