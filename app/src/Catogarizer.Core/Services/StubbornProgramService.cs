namespace Catogarizer.Core.Services;

/// <summary>Why a window was still there after it was asked to close.</summary>
public enum StayReason
{
    /// <summary>It opened a dialog of its own - most likely "Save changes?". Ending it loses that work.</summary>
    AsksYou,

    /// <summary>The window is on screen and nothing answered - the program ignores the request or is stuck.</summary>
    StillOpen,

    /// <summary>The window is out of sight: the program keeps running in the tray or the background.</summary>
    Background,
}

/// <summary>What ending a program did to its process.</summary>
public enum ProgramEndOutcome
{
    Ended,
    /// <summary>It was gone already - nothing left to do, which is what the user wanted.</summary>
    AlreadyGone,
    /// <summary>Catogarizer never ends these (itself, the Windows shell and system processes).</summary>
    Protected,
    /// <summary>The program runs with more rights than Catogarizer, which never asks for admin.</summary>
    AccessDenied,
    Failed,
}

/// <summary>The other windows of a program that live in a category the user did not ask to close.</summary>
public sealed record SharedWindows(string CategoryName, int Count);

/// <summary>
/// A program that kept windows open after "Close windows". <see cref="Windows"/> are the ones that stayed;
/// <see cref="Shared"/> are its windows elsewhere, which end with it because a process cannot be ended in part.
/// </summary>
public sealed record StubbornProgram(int ProcessId, string ProcessName, IReadOnlyList<OpenWindowInfo> Windows,
    IReadOnlyList<SharedWindows> Shared, StayReason Reason, bool IsProtected);

public sealed record ProgramEndResult(StubbornProgram Program, ProgramEndOutcome Outcome)
{
    public bool IsDone => Outcome is ProgramEndOutcome.Ended or ProgramEndOutcome.AlreadyGone;
}

/// <summary>
/// "End program" for the windows that did not close politely (CONCEPT.md "Resource saving"). Always an explicit,
/// per-program user action after a normal close was tried - never part of a switch or a close (PRINCIPLES.md value 1).
///
/// A window cannot be killed on its own: the process behind it ends, and with it every other window it owns. That
/// is why <see cref="Describe"/> names those windows first, and why the shell and Catogarizer itself are off limits.
/// </summary>
public sealed class StubbornProgramService
{
    /// <summary>Processes that must never be ended from here: ending them takes the desktop, the session or the OS with them.</summary>
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "csrss", "winlogon", "wininit", "services", "lsass", "smss", "svchost", "system", "idle",
        "sihost", "fontdrvhost", "ctfmon", "taskhostw", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
        // Owns the window of every Store app: ending it would take unrelated apps (Settings, Calculator) along.
        "ApplicationFrameHost",
    };

    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly IWindowManager _windowManager;
    private readonly int _ownProcessId;

    public StubbornProgramService(LibraryService library, CategorySwitcher switcher, IWindowManager windowManager, int ownProcessId)
    {
        _library = library;
        _switcher = switcher;
        _windowManager = windowManager;
        _ownProcessId = ownProcessId;
    }

    /// <summary>Whether a process may be ended from here at all.</summary>
    public bool IsProtected(int processId, string processName) =>
        processId == _ownProcessId || ProtectedProcesses.Contains(Path.GetFileNameWithoutExtension(processName));

    /// <summary>
    /// Groups the windows that are still open by program. A window that closed in the meantime drops out, so the
    /// list is always what is true now - never what was true when the report was made.
    /// </summary>
    public IReadOnlyList<StubbornProgram> Describe(IEnumerable<OpenWindowInfo> windows)
    {
        var open = windows.Where(w => _windowManager.IsWindowOpen(w.Handle)).ToList();
        if (open.Count == 0) return [];

        var sessions = _switcher.GetSessions();
        var programs = new List<StubbornProgram>();
        foreach (var group in open.GroupBy(w => LivePid(w)).Where(g => g.Key != 0))
        {
            var members = group.ToList();
            var handles = members.Select(w => w.Handle).ToHashSet();
            var name = members[0].ProcessName;
            programs.Add(new StubbornProgram(group.Key, name, members, SharedElsewhere(sessions, group.Key, handles),
                ReasonOf(members), IsProtected(group.Key, name)));
        }
        return programs;
    }

    /// <summary>Ends the program. Never kills a protected one; the caller decides when to ask the user first.</summary>
    public ProgramEndResult End(StubbornProgram program)
    {
        if (program.IsProtected) return new ProgramEndResult(program, ProgramEndOutcome.Protected);

        // The process is ended through any window that is still open; if none is, it already went its own way.
        var window = program.Windows.FirstOrDefault(w => _windowManager.IsWindowOpen(w.Handle));
        if (window is null) return new ProgramEndResult(program, ProgramEndOutcome.AlreadyGone);

        // The handle must still belong to the program we showed the user - handles are reused by Windows.
        if (_windowManager.GetProcessId(window.Handle) != program.ProcessId)
            return new ProgramEndResult(program, ProgramEndOutcome.AlreadyGone);

        var outcome = _windowManager.ForceKill(window.Handle) switch
        {
            ForceKillOutcome.Ended => ProgramEndOutcome.Ended,
            ForceKillOutcome.AlreadyGone => ProgramEndOutcome.AlreadyGone,
            ForceKillOutcome.AccessDenied => ProgramEndOutcome.AccessDenied,
            _ => ProgramEndOutcome.Failed,
        };
        return new ProgramEndResult(program, outcome);
    }

    private int LivePid(OpenWindowInfo window)
    {
        var pid = _windowManager.GetProcessId(window.Handle);
        return pid != 0 ? pid : window.ProcessId;
    }

    private StayReason ReasonOf(IReadOnlyList<OpenWindowInfo> windows)
    {
        if (windows.Any(w => _windowManager.HasOpenDialog(w.Handle))) return StayReason.AsksYou;
        return windows.Any(w => _windowManager.IsWindowVisible(w.Handle)) ? StayReason.StillOpen : StayReason.Background;
    }

    private IReadOnlyList<SharedWindows> SharedElsewhere(IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions,
        int processId, HashSet<IntPtr> own)
    {
        var shared = new List<SharedWindows>();
        foreach (var (categoryId, list) in sessions)
        {
            var count = list.Count(w => !own.Contains(w.Handle) && _windowManager.IsWindowOpen(w.Handle) && LivePid(w) == processId);
            if (count > 0) shared.Add(new SharedWindows(_library.NameOf(categoryId), count));
        }
        return shared;
    }
}
