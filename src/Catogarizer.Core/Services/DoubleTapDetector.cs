namespace Catogarizer.Core.Services;

/// <summary>
/// Tells a single hotkey press from the second press of a quick double-tap. A third quick
/// press starts a new sequence rather than counting as another double.
/// </summary>
public sealed class DoubleTapDetector
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(350);

    private readonly Func<long> _nowMilliseconds;
    private readonly long _windowMilliseconds;
    private long? _lastSingleTap;

    public DoubleTapDetector(Func<long>? nowMilliseconds = null, TimeSpan? window = null)
    {
        _nowMilliseconds = nowMilliseconds ?? (() => Environment.TickCount64);
        _windowMilliseconds = (long)(window ?? DefaultWindow).TotalMilliseconds;
    }

    /// <summary>True if this press completes a double-tap.</summary>
    public bool RegisterTap()
    {
        var now = _nowMilliseconds();
        var isDouble = _lastSingleTap is { } last && now - last <= _windowMilliseconds;
        _lastSingleTap = isDouble ? null : now;
        return isDouble;
    }
}
