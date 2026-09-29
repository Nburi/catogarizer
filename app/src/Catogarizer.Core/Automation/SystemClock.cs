namespace Catogarizer.Core.Automation;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
