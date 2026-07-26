using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public interface IProcessLauncher
{
    Task<LaunchedApp> LaunchAsync(AppEntry entry, CancellationToken cancellationToken = default);
    LaunchedApp? FindRunning(AppEntry entry);
}
