namespace Catogarizer.Core.Models;

/// <summary>
/// Whether the apps of a parked category may be suspended (CONCEPT.md "Resource saving"). Suspended
/// apps stop completely: chat apps miss messages, downloads and syncs pause until the category is shown again.
/// </summary>
public enum FreezeMode
{
    /// <summary>Slowed down and, when memory is tight, paged out - never suspended.</summary>
    Never = 0,

    /// <summary>Suspended only when paging out was not enough.</summary>
    WhenTight = 1,

    /// <summary>Suspended shortly after the category is parked.</summary>
    Immediately = 2,
}
