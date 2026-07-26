namespace Catogarizer.Core.Services;

public sealed record LaunchedApp(int ProcessId, nint MainWindowHandle, string ExecutablePath);
