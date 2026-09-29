using Catogarizer.Core.Automation;

namespace Catogarizer.Core.Tests;

public sealed class TriggerPreviewTests
{
    // Monday 28 Sep 2026, 10:00
    private static readonly DateTime Now = new(2026, 9, 28, 10, 0, 0);

    private static Trigger Time(string name, string at, params DayOfWeek[] days) =>
        new()
        {
            Name = name, Type = TriggerType.Time, TimeOfDay = at, DaysOfWeek = days.ToList(),
            Actions = [new TriggerAction { Type = TriggerActionType.OpenCategory, CategoryId = Guid.NewGuid() }],
        };

    [Fact]
    public void Next_IgnoresTriggersWithoutActions()
    {
        var empty = Time("Wrap-up", "16:30");
        empty.Actions.Clear();

        Assert.Null(TriggerPreview.Next([empty], Now));
    }

    [Fact]
    public void Next_PicksTheSoonestUpcomingTimeTrigger()
    {
        var next = TriggerPreview.Next([Time("Wrap-up", "16:30"), Time("Lunch", "12:00")], Now);

        Assert.Equal("Lunch", next!.Trigger.Name);
        Assert.Equal(new DateTime(2026, 9, 28, 12, 0, 0), next.At);
    }

    [Fact]
    public void Next_ATimeAlreadyPassedToday_IsTomorrow()
    {
        var next = TriggerPreview.Next([Time("Morning", "08:30")], Now);

        Assert.Equal(new DateTime(2026, 9, 29, 8, 30, 0), next!.At);
    }

    [Fact]
    public void Next_RespectsDaysOfWeek()
    {
        // Only Wednesdays: more than 24 h away from Monday.
        Assert.Null(TriggerPreview.Next([Time("Sport", "18:00", DayOfWeek.Wednesday)], Now));
    }

    [Fact]
    public void Next_IgnoresDisabledStartupAndBrokenTriggers()
    {
        var disabled = Time("Off", "11:00");
        disabled.IsEnabled = false;
        var startup = new Trigger { Name = "Boot", Type = TriggerType.Startup };
        var broken = Time("Broken", "25:99");

        Assert.Null(TriggerPreview.Next([disabled, startup, broken], Now));
    }
}
