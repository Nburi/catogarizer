namespace Catogarizer.Core.Services;

public sealed record OpenWindowInfo(nint Handle, string Title, int ProcessId);
