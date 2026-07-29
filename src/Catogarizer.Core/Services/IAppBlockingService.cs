using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public interface IAppBlockingService
{
    /// <summary>Raised with the blocked app's display name whenever one gets closed out from under the user.</summary>
    event Action<string>? AppBlocked;

    void Start();
    void Stop();

    void ActivateCategory(Guid categoryId, IReadOnlyList<BlockedApp> blockedApps);
    void DeactivateCategory(Guid categoryId);
}
