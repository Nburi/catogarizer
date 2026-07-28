using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

internal sealed class FakeWindowManager : IWindowManager
{
    public List<(nint Handle, WindowRect Rect)> MoveResizeCalls { get; } = new();
    public List<nint> MinimizeCalls { get; } = new();
    public List<nint> RestoreCalls { get; } = new();
    public List<nint> CloseCalls { get; } = new();

    /// <summary>Lets tests simulate a window that ignores the request (e.g. minimize-to-tray apps).</summary>
    public bool MinimizeSucceeds { get; set; } = true;
    public bool CloseSucceeds { get; set; } = true;

    public void MoveResize(nint windowHandle, WindowRect rect) => MoveResizeCalls.Add((windowHandle, rect));

    public Task<bool> MinimizeAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        MinimizeCalls.Add(windowHandle);
        return Task.FromResult(MinimizeSucceeds);
    }

    public void Restore(nint windowHandle) => RestoreCalls.Add(windowHandle);

    public Task<bool> CloseAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        CloseCalls.Add(windowHandle);
        return Task.FromResult(CloseSucceeds);
    }

    public WindowRect? GetRect(nint windowHandle) => null;
}
