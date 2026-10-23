using System.Runtime.InteropServices;
using Catogarizer.Core.Resources;
using static Catogarizer.Win32.Interop.NativeMethods;

namespace Catogarizer.Win32;

/// <summary>
/// CPU load from <c>GetSystemTimes</c> (plain Win32, like <see cref="SystemMemory"/>) and graphics load from the
/// "GPU Engine" performance counters, which Windows keeps per process and engine for every card. Both are rates, so
/// a figure needs two looks: the first sample reads -1. Looks closer than <see cref="MinInterval"/> answer from the
/// last one - the counters are sampled over time and a few milliseconds say nothing.
///
/// No admin rights; a machine without the counters (old Windows, a driver without WDDM 2.0) just never reports a GPU.
/// Sample is not thread safe (the governor only calls it under its own lock); opening the GPU query runs on a
/// pool thread and publishes through <c>_gpuReady</c>.
/// </summary>
public sealed class SystemLoad : ISystemLoad
{
    private const string GpuCounterPath = @"\GPU Engine(*)\Utilization Percentage";
    private const int MaxBufferRetries = 3;
    private const long MinInterval = 1000;

    private long _lastSampleAt = -MinInterval;
    private SystemLoadSample _last = SystemLoadSample.Unreadable;

    private (long Idle, long Kernel, long User)? _previousTimes;

    private IntPtr _query;
    private IntPtr _gpuCounter;
    private Task? _opening;
    private volatile bool _gpuReady;
    private volatile bool _gpuUnavailable;

    public SystemLoadSample Sample()
    {
        var now = Environment.TickCount64;
        if (now - _lastSampleAt < MinInterval) return _last;

        _lastSampleAt = now;
        try
        {
            var gpu = ReadGpu();
            _last = new SystemLoadSample(ReadCpu(), gpu?.Percent ?? -1, gpu?.ByProcess ?? new Dictionary<int, int>());
        }
        catch (Exception)
        {
            // A load figure is never worth a crash; the next look tries again.
            _last = SystemLoadSample.Unreadable;
        }
        return _last;
    }

    private int ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return -1;

        var previous = _previousTimes;
        _previousTimes = (idle, kernel, user);
        if (previous is not { } before) return -1;

        // Kernel time includes the idle time.
        var total = (kernel - before.Kernel) + (user - before.User);
        if (total <= 0) return -1;
        var busy = total - (idle - before.Idle);
        return (int)Math.Round(Math.Clamp(busy * 100.0 / total, 0, 100));
    }

    private (int Percent, IReadOnlyDictionary<int, int> ByProcess)? ReadGpu()
    {
        if (_gpuUnavailable) return null;

        if (!_gpuReady)
        {
            // Listing every engine of every process takes about half a second the first time. That must not
            // stall whoever asked (the governor holds its lock, the UI may be waiting for it): open in the
            // background and report "unreadable" until it is done.
            _opening ??= Task.Run(() =>
            {
                if (OpenGpuQuery()) _gpuReady = true;
                else _gpuUnavailable = true;
            });
            return null;
        }

        if (PdhCollectQueryData(_query) != 0) return null;

        var counters = ReadCounterArray();
        return counters is null ? null : GpuEngineCounters.Aggregate(counters);
    }

    private bool OpenGpuQuery()
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out var query) != 0) return false;
        if (PdhAddEnglishCounterW(query, GpuCounterPath, IntPtr.Zero, out var counter) != 0)
        {
            PdhCloseQuery(query);
            return false;
        }

        _query = query;
        _gpuCounter = counter;
        // The first collection only starts the clock; the next one has something to compare with.
        PdhCollectQueryData(_query);
        return true;
    }

    private List<(string Instance, double Value)>? ReadCounterArray()
    {
        uint size = 0;
        for (var attempt = 0; attempt < MaxBufferRetries; attempt++)
        {
            var buffer = size == 0 ? IntPtr.Zero : Marshal.AllocHGlobal((int)size);
            try
            {
                var status = PdhGetFormattedCounterArrayW(_gpuCounter, PDH_FMT_DOUBLE, ref size, out var count, buffer);
                if (status == PDH_MORE_DATA)
                {
                    // Instances come and go between the two calls: ask again with the size it wants now.
                    continue;
                }
                if (status != 0 || buffer == IntPtr.Zero) return null;

                var itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM_W>();
                var result = new List<(string, double)>((int)count);
                for (var i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM_W>(buffer + i * itemSize);
                    // CStatus 0 / 1: valid data / new data.
                    if (item.FmtValue.CStatus > 1 || Marshal.PtrToStringUni(item.szName) is not { } name) continue;
                    result.Add((name, item.FmtValue.doubleValue));
                }
                return result;
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }
        return null;
    }
}
