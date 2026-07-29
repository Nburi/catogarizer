namespace Catogarizer.Core.Integrations;

public sealed record TodoItem(string Id, string Title, bool IsCompleted, DateTimeOffset? DueDate);
