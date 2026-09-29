namespace Catogarizer.Core.Automation;

/// <summary>
/// E.g. "Morning setup" (Type=Startup, Actions=[OpenCategory Work]), or "Wind down"
/// (Type=Time, TimeOfDay="18:00", DaysOfWeek=[Mon..Fri], Actions=[CloseApps ...]).
/// </summary>
public sealed class Trigger
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public TriggerType Type { get; set; }

    /// <summary>"HH:mm", 24-hour. Only meaningful when Type == Time.</summary>
    public string? TimeOfDay { get; set; }

    /// <summary>Empty means every day. Only meaningful when Type == Time.</summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    public List<TriggerAction> Actions { get; set; } = new();
}
