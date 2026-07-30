using Catogarizer.Core.Automation;

namespace Catogarizer.Core.Tests;

public sealed class TriggerSchedulerServiceTests
{
    private static readonly DateTime Wednesday0800 = new(2026, 7, 29, 8, 0, 0); // 2026-07-29 is a Wednesday

    [Fact]
    public void CheckAndFire_ExactTimeMatch_Fires()
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("08:00");

        scheduler.CheckAndFire(Wednesday0800, [trigger]);

        Assert.Single(fired);
    }

    [Fact]
    public void CheckAndFire_TimeDoesNotMatch_DoesNotFire()
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("09:00");

        scheduler.CheckAndFire(Wednesday0800, [trigger]);

        Assert.Empty(fired);
    }

    [Fact]
    public void CheckAndFire_DisabledTrigger_DoesNotFire()
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("08:00");
        trigger.IsEnabled = false;

        scheduler.CheckAndFire(Wednesday0800, [trigger]);

        Assert.Empty(fired);
    }

    [Theory]
    [InlineData(TriggerType.Startup)]
    [InlineData(TriggerType.Manual)]
    public void CheckAndFire_NonTimeType_NeverFiresAutomatically(TriggerType type)
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("08:00");
        trigger.Type = type;

        scheduler.CheckAndFire(Wednesday0800, [trigger]);

        Assert.Empty(fired);
    }

    [Fact]
    public void CheckAndFire_DayOfWeekFilter_OnlyFiresOnMatchingDays()
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("08:00");
        trigger.DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday]; // Wednesday isn't in the list

        scheduler.CheckAndFire(Wednesday0800, [trigger]);

        Assert.Empty(fired);
    }

    [Fact]
    public void CheckAndFire_SameCalendarDayTwice_FiresOnlyOnce()
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("08:00");

        scheduler.CheckAndFire(Wednesday0800, [trigger]);
        scheduler.CheckAndFire(Wednesday0800.AddMinutes(1), [trigger]); // later same day, matched time already passed

        Assert.Single(fired);
    }

    [Fact]
    public void CheckAndFire_NextCalendarDay_FiresAgain()
    {
        var fired = new List<Trigger>();
        var scheduler = new TriggerSchedulerService(new FakeClock(), t => fired.Add(t));
        var trigger = MakeTimeTrigger("08:00");

        scheduler.CheckAndFire(Wednesday0800, [trigger]);
        scheduler.CheckAndFire(Wednesday0800.AddDays(1), [trigger]);

        Assert.Equal(2, fired.Count);
    }

    private static Trigger MakeTimeTrigger(string timeOfDay) =>
        new() { Name = "Test", Type = TriggerType.Time, TimeOfDay = timeOfDay };

    private sealed class FakeClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
