using System.Globalization;

namespace Catogarizer.Core.Automation;

public sealed record UpcomingTrigger(Trigger Trigger, DateTime At);

public static class TriggerPreview
{
    /// <summary>The next enabled time trigger within <paramref name="horizon"/> (default 24 h) of <paramref name="now"/>, or null.</summary>
    public static UpcomingTrigger? Next(IEnumerable<Trigger> triggers, DateTime now, TimeSpan? horizon = null)
    {
        var limit = now + (horizon ?? TimeSpan.FromHours(24));
        UpcomingTrigger? best = null;

        foreach (var trigger in triggers.Where(t => t.IsEnabled && t.Type == TriggerType.Time))
        {
            if (!TimeOnly.TryParseExact(trigger.TimeOfDay, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                continue;

            for (var day = 0; day <= 1; day++)
            {
                var date = now.Date.AddDays(day);
                var at = date + time.ToTimeSpan();
                if (at <= now || at > limit) continue;
                if (trigger.DaysOfWeek.Count > 0 && !trigger.DaysOfWeek.Contains(date.DayOfWeek)) continue;
                if (best is null || at < best.At) best = new UpcomingTrigger(trigger, at);
                break;
            }
        }
        return best;
    }
}
