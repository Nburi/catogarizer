using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

internal sealed class FakeProcessLauncher : IProcessLauncher
{
    public Dictionary<Guid, LaunchedApp> RunningApps { get; } = new();
    public List<Guid> LaunchedIds { get; } = new();
    public Func<AppEntry, LaunchedApp>? LaunchResultFactory { get; set; }
    public Func<AppEntry, Exception>? ThrowOnLaunch { get; set; }

    public Task<LaunchedApp> LaunchAsync(AppEntry entry, CancellationToken cancellationToken = default)
    {
        if (ThrowOnLaunch is not null)
            throw ThrowOnLaunch(entry);

        LaunchedIds.Add(entry.Id);
        var result = LaunchResultFactory?.Invoke(entry) ?? new LaunchedApp(1, 12345, entry.ExecutablePath);
        return Task.FromResult(result);
    }

    public LaunchedApp? FindRunning(AppEntry entry) =>
        RunningApps.TryGetValue(entry.Id, out var app) ? app : null;
}
