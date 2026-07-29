using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeConfigStore : IConfigStore
{
    private AppConfig _config = new();

    public int SaveCount { get; private set; }

    public AppConfig Load() => _config;

    public void Save(AppConfig config)
    {
        _config = config;
        SaveCount++;
    }
}
