using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public sealed record AppBlockedEventArgs(string ProcessName, DateTimeOffset Timestamp);

public interface IAppBlocker
{
    event EventHandler<AppBlockedEventArgs>? AppBlocked;
    void Start(IReadOnlyList<BlockedApp> blockList);
    void Stop();
    void UpdateBlockList(IReadOnlyList<BlockedApp> blockList);
}
