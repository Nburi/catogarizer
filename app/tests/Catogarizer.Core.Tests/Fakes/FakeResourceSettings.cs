using Catogarizer.Core.Models;
using Catogarizer.Core.Resources;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeResourceSettings : IResourceSettings
{
    public bool Enabled { get; set; } = true;
    public HashSet<string> Pinned { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Freezable { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<Guid, FreezeMode> Modes { get; } = [];

    public bool IsPinned(string processName) => Pinned.Contains(processName);

    public bool IsAppFreezable(string processName) => Freezable.Contains(processName);

    public FreezeMode FreezeModeOf(Guid categoryId) => Modes.GetValueOrDefault(categoryId, FreezeMode.Never);
}
