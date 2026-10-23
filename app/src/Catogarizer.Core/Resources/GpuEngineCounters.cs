using System.Globalization;

namespace Catogarizer.Core.Resources;

/// <summary>
/// Turns the "GPU Engine" counters of Windows into the two figures the governor needs. One counter exists per
/// process and engine, named like <c>pid_1234_luid_0x00000000_0x0000F3A2_phys_0_eng_0_engtype_3D</c>. The load
/// of the card is that of its busiest engine (what Task Manager shows), and a process is measured the same way.
/// Pure, so the Win32 side only has to deliver (name, value) pairs.
/// </summary>
public static class GpuEngineCounters
{
    private const string PidPrefix = "pid_";

    /// <summary>
    /// The busiest engine's load, and per process the load of its busiest engine. Names that do not look
    /// like an engine counter are skipped; values are clamped to 0-100.
    /// </summary>
    public static (int Percent, IReadOnlyDictionary<int, int> ByProcess) Aggregate(IEnumerable<(string Instance, double Value)> counters)
    {
        var perEngine = new Dictionary<string, double>();
        var perProcess = new Dictionary<int, Dictionary<string, double>>();

        foreach (var (instance, value) in counters)
        {
            if (!TryParse(instance, out var pid, out var engine) || !double.IsFinite(value) || value <= 0) continue;

            perEngine[engine] = perEngine.GetValueOrDefault(engine) + value;
            if (!perProcess.TryGetValue(pid, out var engines)) perProcess[pid] = engines = [];
            engines[engine] = engines.GetValueOrDefault(engine) + value;
        }

        var percent = perEngine.Count == 0 ? 0 : Clamp(perEngine.Values.Max());
        var byProcess = perProcess
            .Select(p => (Pid: p.Key, Percent: Clamp(p.Value.Values.Max())))
            .Where(p => p.Percent > 0)
            .ToDictionary(p => p.Pid, p => p.Percent);
        return (percent, byProcess);
    }

    /// <summary>Splits a counter name into the process and the engine (everything after the process id).</summary>
    public static bool TryParse(string instance, out int pid, out string engine)
    {
        pid = 0;
        engine = "";
        if (!instance.StartsWith(PidPrefix, StringComparison.Ordinal)) return false;

        var end = instance.IndexOf('_', PidPrefix.Length);
        if (end < 0) return false;
        if (!int.TryParse(instance.AsSpan(PidPrefix.Length, end - PidPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out pid))
            return false;

        engine = instance[(end + 1)..];
        return engine.Length > 0;
    }

    private static int Clamp(double value) => (int)Math.Round(Math.Clamp(value, 0, 100));
}
