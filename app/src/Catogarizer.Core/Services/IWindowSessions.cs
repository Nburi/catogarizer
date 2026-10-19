namespace Catogarizer.Core.Services;

/// <summary>The read side of category switching: who is active and which windows each category holds.</summary>
public interface IWindowSessions
{
    Guid ActiveCategoryId { get; }

    /// <summary>
    /// Raised after a switch, a reset, or a change to any session's window set.
    /// Can fire on a background thread (the window watcher's), and from inside a switch - handlers must not block.
    /// </summary>
    event Action? StateChanged;

    /// <summary>Still-open windows per category, with their current titles.</summary>
    IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> GetSessions();
}
