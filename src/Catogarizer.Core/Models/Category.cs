namespace Catogarizer.Core.Models;

/// <summary>
/// A named, user-ordered group of apps. An app can belong to more than one
/// category (referenced by id, not owned), so <see cref="AppIds"/> holds
/// <see cref="AppEntry.Id"/> values rather than embedded app objects.
/// <see cref="BlockedAppIds"/> holds <see cref="BlockedApp.Id"/> values that
/// are only enforced while this category is the active one.
/// </summary>
public sealed class Category
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<Guid> AppIds { get; set; } = new();
    public List<Guid> BlockedAppIds { get; set; } = new();

    /// <summary>OKLCH hue in degrees; the active theme decides lightness and chroma. Null only in configs saved before colors existed.</summary>
    public double? Hue { get; set; }
}
