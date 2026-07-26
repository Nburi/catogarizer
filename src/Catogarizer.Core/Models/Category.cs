namespace Catogarizer.Core.Models;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<AppEntry> Apps { get; set; } = new();
}
