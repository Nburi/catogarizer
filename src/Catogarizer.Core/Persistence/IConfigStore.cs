using Catogarizer.Core.Models;

namespace Catogarizer.Core.Persistence;

public interface IConfigStore
{
    Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default);
}
