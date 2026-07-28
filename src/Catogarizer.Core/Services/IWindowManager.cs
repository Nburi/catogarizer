using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public interface IWindowManager
{
    void MoveResize(nint windowHandle, WindowRect rect);

    /// <summary>Requests minimize and waits to confirm the window actually became iconic before returning.</summary>
    Task<bool> MinimizeAsync(nint windowHandle, CancellationToken cancellationToken = default);

    void Restore(nint windowHandle);

    /// <summary>Requests close and waits to confirm the window was actually destroyed before returning.</summary>
    Task<bool> CloseAsync(nint windowHandle, CancellationToken cancellationToken = default);

    /// <summary>Reads a window's current position/size, for the "capture window position" helper.</summary>
    WindowRect? GetRect(nint windowHandle);
}
