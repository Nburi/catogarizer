namespace Catogarizer.Core.Models;

/// <summary>
/// An app that stays visible across every category switch instead of being
/// hidden with the rest of the outgoing session (e.g. Spotify, WhatsApp).
/// Global, not per-category - matches the same name-or-path matching
/// convention as <see cref="BlockedApp"/>.
/// </summary>
public sealed class PinnedApp
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProcessNameOrPath { get; set; } = string.Empty;
}
