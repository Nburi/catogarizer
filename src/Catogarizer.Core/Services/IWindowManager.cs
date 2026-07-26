using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public interface IWindowManager
{
    void MoveResize(nint windowHandle, WindowRect rect);
    void Minimize(nint windowHandle);
    void Restore(nint windowHandle);
    void Close(nint windowHandle);
}
