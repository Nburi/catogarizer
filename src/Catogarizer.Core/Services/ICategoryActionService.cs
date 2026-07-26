using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// Orchestrates opening/closing/minimizing a whole category. Called directly by the
/// UI today, and intended as the same entry point a future automation trigger would call.
/// </summary>
public interface ICategoryActionService
{
    Task OpenAsync(Category category, CancellationToken cancellationToken = default);
    Task CloseAsync(Category category, CancellationToken cancellationToken = default);
    Task MinimizeAsync(Category category, CancellationToken cancellationToken = default);
}
