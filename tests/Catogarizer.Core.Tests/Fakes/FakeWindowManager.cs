using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

internal sealed class FakeWindowManager : IWindowManager
{
    public List<(nint Handle, WindowRect Rect)> MoveResizeCalls { get; } = new();
    public List<nint> MinimizeCalls { get; } = new();
    public List<nint> RestoreCalls { get; } = new();
    public List<nint> CloseCalls { get; } = new();

    public void MoveResize(nint windowHandle, WindowRect rect) => MoveResizeCalls.Add((windowHandle, rect));
    public void Minimize(nint windowHandle) => MinimizeCalls.Add(windowHandle);
    public void Restore(nint windowHandle) => RestoreCalls.Add(windowHandle);
    public void Close(nint windowHandle) => CloseCalls.Add(windowHandle);
    public WindowRect? GetRect(nint windowHandle) => null;
}
