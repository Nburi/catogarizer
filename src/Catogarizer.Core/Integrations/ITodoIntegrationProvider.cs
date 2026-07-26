namespace Catogarizer.Core.Integrations;

/// <summary>
/// Seam for a future to-do list API integration - no concrete implementation
/// exists yet, and nothing in the app references this interface. Whichever
/// provider (Microsoft To Do, Todoist, etc.) gets built later implements this
/// directly rather than requiring changes to Core or the UI's data flow.
/// </summary>
public interface ITodoIntegrationProvider
{
    Task<IReadOnlyList<TodoItem>> GetItemsAsync(CancellationToken cancellationToken = default);
}
