namespace Catogarizer.Core.Resources;

/// <summary>What one category costs right now, over the whole process tree of its windows.</summary>
/// <param name="Level">The lowest level of any of its trees: "Asleep" only when everything in it is.</param>
/// <param name="UntouchableProcesses">Processes that could not be slowed down (administrator, protected).</param>
public sealed record CategoryResourceUsage(Guid CategoryId, long PrivateBytes, double CpuPercent, ResourceLevel Level, int UntouchableProcesses);

/// <summary>Memory given back by putting parked categories to sleep - what the "Freed 1.4 GB" pill says.</summary>
public sealed record RelievedMemory(long FreedBytes, IReadOnlyList<Guid> CategoryIds);
