namespace Catogarizer.Core.Automation;

/// <summary>
/// Polls the clock rather than reacting to an OS timer event - same "polling over eventing"
/// call as AppBlockingService/ProcessWatcher elsewhere in Core (see STACK.md), and keeps this
/// class Win32-free/unit-testable. Reads the trigger list fresh via <c>triggersProvider</c> on
/// every tick, so edits made in the UI take effect without an app restart.
/// </summary>
public sealed class TriggerSchedulerService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    private readonly IClock _clock;
    private readonly Action<Trigger> _onFire;
    private readonly Dictionary<Guid, DateOnly> _lastFiredDate = new();
    private Timer? _timer;

    public TriggerSchedulerService(IClock clock, Action<Trigger> onFire)
    {
        _clock = clock;
        _onFire = onFire;
    }

    public void Start(Func<IReadOnlyList<Trigger>> triggersProvider)
    {
        _timer = new Timer(_ => CheckAndFire(_clock.Now, triggersProvider()), null, PollInterval, PollInterval);
    }

    /// <summary>Exposed so tests can drive it directly with fixed DateTime values, no real timer needed.</summary>
    public void CheckAndFire(DateTime now, IReadOnlyList<Trigger> triggers)
    {
        var today = DateOnly.FromDateTime(now);
        var currentTime = now.ToString("HH:mm");

        foreach (var trigger in triggers)
        {
            if (!trigger.IsEnabled || trigger.Type != TriggerType.Time) continue;
            if (trigger.TimeOfDay != currentTime) continue;
            if (trigger.DaysOfWeek.Count > 0 && !trigger.DaysOfWeek.Contains(now.DayOfWeek)) continue;
            if (_lastFiredDate.TryGetValue(trigger.Id, out var lastFired) && lastFired == today) continue;

            _lastFiredDate[trigger.Id] = today;
            _onFire(trigger);
        }
    }

    public void Dispose() => _timer?.Dispose();
}
