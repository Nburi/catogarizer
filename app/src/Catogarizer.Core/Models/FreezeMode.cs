namespace Catogarizer.Core.Models;

/// <summary>
/// Whether the apps of a parked category may be suspended (CONCEPT.md "Resource saving"). Suspended
/// apps stop completely: chat apps miss messages, downloads and syncs pause until the category is shown again.
/// </summary>
public enum FreezeMode
{
    /// <summary>Slowed down and, when memory is tight, paged out - never suspended.</summary>
    Never = 0,

    /// <summary>
    /// Suspended only when the PC is under load: paging out was not enough for memory, or the CPU or GPU stay maxed
    /// out and these apps use a share of it. (Named for memory, the first trigger; the name is saved in config files.)
    /// </summary>
    WhenTight = 1,

    /// <summary>Suspended shortly after the category is parked.</summary>
    Immediately = 2,
}
