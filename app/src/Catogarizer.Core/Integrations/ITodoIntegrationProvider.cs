namespace Catogarizer.Core.Integrations;

/// <summary>
/// Not wired to any UI yet - data model/interface only, so a real provider
/// (whichever to-do API gets picked) can be dropped in later without
/// reshaping anything else (CONCEPT.md's "future, prepare for").
/// </summary>
public interface ITodoIntegrationProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<TodoItem>> GetItemsAsync(CancellationToken cancellationToken = default);
}
