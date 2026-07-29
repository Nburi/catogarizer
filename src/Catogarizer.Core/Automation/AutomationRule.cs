namespace Catogarizer.Core.Automation;

/// <summary>
/// E.g. "open Deep Work at 09:00" (TriggerType=TimeOfDay, TriggerValue="09:00",
/// ActionType=OpenCategory, CategoryId=...). TriggerValue's expected format
/// depends on TriggerType (unconstrained here on purpose - not built yet).
/// </summary>
public sealed class AutomationRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public AutomationTriggerType TriggerType { get; set; }
    public string TriggerValue { get; set; } = string.Empty;
    public AutomationActionType ActionType { get; set; }
    public Guid CategoryId { get; set; }
}
