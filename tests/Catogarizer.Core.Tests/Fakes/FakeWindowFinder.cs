using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeWindowFinder : IWindowFinder
{
    public Dictionary<int, IntPtr?> MainWindowByPid { get; } = new();
    public List<OpenWindowInfo> RunningWindows { get; } = new();

    public IntPtr? FindMainWindow(int processId, IReadOnlyList<string> titleCandidates, TimeSpan timeout) =>
        MainWindowByPid.TryGetValue(processId, out var hwnd) ? hwnd : null;

    public IReadOnlyList<OpenWindowInfo> FindAllRunningWindows(IReadOnlyList<string> processNameCandidates, IReadOnlyList<string> titleCandidates) =>
        RunningWindows.Where(w =>
            processNameCandidates.Contains(w.ProcessName, StringComparer.OrdinalIgnoreCase) ||
            titleCandidates.Any(t => w.Title.Contains(t, StringComparison.OrdinalIgnoreCase))
        ).ToList();
}
