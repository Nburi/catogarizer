namespace Catogarizer.Core.Services;

/// <summary>
/// A display monitor. <see cref="Id"/> is a stable device identifier (not a
/// positional index, which can shift when monitors are added/removed/reordered).
/// </summary>
public sealed record MonitorInfo(string Id, int X, int Y, int Width, int Height, bool IsPrimary);
