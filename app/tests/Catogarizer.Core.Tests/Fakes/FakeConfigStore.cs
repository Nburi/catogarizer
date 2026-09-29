using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeConfigStore : IConfigStore
{
    private AppConfig _config;

    public FakeConfigStore(AppConfig? initial = null) => _config = initial ?? new AppConfig();

    public int SaveCount { get; private set; }

    public AppConfig Load() => _config;

    public void Save(AppConfig config)
    {
        _config = config;
        SaveCount++;
    }
}
