using Catogarizer.Core.Services;

namespace Catogarizer.Core.Tests.Fakes;

public sealed class FakeWindowSessions : IWindowSessions
{
    private int _nextHandle = 1000;
    private readonly Dictionary<Guid, List<OpenWindowInfo>> _sessions = [];

    public Guid ActiveCategoryId { get; set; }

    public event Action? StateChanged;

    /// <summary>Adds a window to a category and returns it.</summary>
    public OpenWindowInfo Add(Guid category, int pid, string processName = "app", string title = "Window")
    {
        var window = new OpenWindowInfo(new IntPtr(_nextHandle++), title, processName, pid);
        if (!_sessions.TryGetValue(category, out var list)) _sessions[category] = list = [];
        list.Add(window);
        return window;
    }

    public void Remove(Guid category, OpenWindowInfo window) => _sessions[category].Remove(window);

    public void RaiseStateChanged() => StateChanged?.Invoke();

    public IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> GetSessions() =>
        _sessions.ToDictionary(p => p.Key, p => (IReadOnlyList<OpenWindowInfo>)p.Value.ToList());
}
