using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeAppBlockingService : IAppBlockingService
{
    public Dictionary<Guid, IReadOnlyList<BlockedApp>> Active { get; } = new();

    public event Action<string>? AppBlocked;

    public void Start() { }
    public void Stop() { }

    public void ActivateCategory(Guid categoryId, IReadOnlyList<BlockedApp> blockedApps) => Active[categoryId] = blockedApps;
    public void DeactivateCategory(Guid categoryId) => Active.Remove(categoryId);

    public IEnumerable<string> BlockedNames => Active.Values.SelectMany(l => l).Select(b => b.Name);

    public void RaiseAppBlocked(string name) => AppBlocked?.Invoke(name);
}
