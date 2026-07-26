using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// Orchestrates opening/closing/minimizing a whole category. Called directly by the
/// UI today, and intended as the same entry point a future automation trigger would call.
/// </summary>
public interface ICategoryActionService
{
    Task<CategoryActionResult> OpenAsync(Category category, CancellationToken cancellationToken = default);
    Task<CategoryActionResult> CloseAsync(Category category, CancellationToken cancellationToken = default);
    Task<CategoryActionResult> MinimizeAsync(Category category, CancellationToken cancellationToken = default);
}
