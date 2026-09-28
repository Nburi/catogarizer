using Catogarizer.Core.Automation;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 9, 28, 9, 0, 0);
}
