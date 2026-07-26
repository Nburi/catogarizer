namespace Catogarizer.Core.Automation;

/// <summary>
/// How an automation rule fires. Only one case exists today because no trigger
/// is actually evaluated yet - this is a seam for the "open a category
/// automatically" feature, not a working scheduler. TriggerConfig on
/// AutomationRule carries whatever trigger-specific data this type eventually
/// needs (e.g. a time-of-day expression), left as an opaque string until a
/// concrete trigger is implemented.
/// </summary>
public enum AutomationTriggerType
{
    Scheduled,
}
