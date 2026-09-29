namespace Catogarizer.Core.Automation;

/// <summary>Abstraction purely so TriggerSchedulerService is testable without real wall-clock waits.</summary>
public interface IClock
{
    DateTime Now { get; }
}
