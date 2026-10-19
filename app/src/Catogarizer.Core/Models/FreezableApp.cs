namespace Catogarizer.Core.Models;

/// <summary>
/// An app the user allowed to be suspended while its windows are parked, whatever category they are in.
/// Same name-or-path matching as <see cref="PinnedApp"/>.
/// </summary>
public sealed class FreezableApp
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProcessNameOrPath { get; set; } = string.Empty;
}
