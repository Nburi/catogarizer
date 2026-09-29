namespace Catogarizer.Core.Services;

/// <summary>A window discovered on screen, whether or not this app launched it.</summary>
public sealed record OpenWindowInfo(IntPtr Handle, string Title, string ProcessName, int ProcessId);
