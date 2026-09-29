namespace Catogarizer.Core.Automation;

/// <summary>
/// Only governs automatic firing - Startup fires on "catogarizer start", Time fires on
/// schedule. Every trigger, regardless of type, can also be run on demand (Manual never
/// fires any other way).
/// </summary>
public enum TriggerType
{
    Startup,
    Time,
    Manual,
}
