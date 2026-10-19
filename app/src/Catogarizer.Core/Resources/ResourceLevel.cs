namespace Catogarizer.Core.Resources;

/// <summary>
/// How much a parked process tree has been put to rest (CONCEPT.md "Resource saving"). Each level
/// includes the ones below it: an asleep tree is also in efficiency mode.
/// </summary>
public enum ResourceLevel
{
    /// <summary>Untouched.</summary>
    Normal = 0,

    /// <summary>Efficiency mode (EcoQoS) and a low priority.</summary>
    Eco = 1,

    /// <summary>Eco, and the memory was paged out to make room.</summary>
    Asleep = 2,

    /// <summary>Suspended. Only ever for apps or categories the user allowed.</summary>
    Frozen = 3,
}
