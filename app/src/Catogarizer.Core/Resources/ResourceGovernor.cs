using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Resources;

/// <summary>
/// Puts parked categories to rest (CONCEPT.md "Resource saving"): Eco whenever parked, paged out under
/// memory pressure, suspended only where the user allowed it. One look at the world per <see cref="Tick"/>:
/// it works out which process trees belong to parked windows only, what each should be, and moves the
/// processes there - and back the moment their category is shown again.
///
/// Process trees, not windows: the category model is per window, but efficiency mode, paging and suspending
/// are per process. A tree that owns any window of the active category (a browser with two windows in two
/// categories), a pinned app, Catogarizer itself, a system process or anything playing sound is left alone -
/// sound only while the category keeps it awake (the default) and the app is not explicitly allowed to freeze.
///
/// Every applied state is in the <see cref="IThrottleLedger"/> before a process is suspended, so a crash
/// never leaves one frozen (PRINCIPLES.md value 1). Polling, like every other watcher here (STACK.md).
/// </summary>
public sealed class ResourceGovernor : IDisposable
{
    /// <summary>Memory use that starts a pressure episode.</summary>
    public const int TightLoad = 80;

    /// <summary>Memory use at or below which the episode is over.</summary>
    public const int CalmLoad = 70;

    /// <summary>Memory use that counts as "paging out was not enough".</summary>
    public const int SevereLoad = 85;

    /// <summary>Trimming stops once memory has fallen below this.</summary>
    public const int RelievedLoad = 75;

    /// <summary>CPU or GPU use that starts a busy episode. Like <see cref="SevereLoad"/> it must last <see cref="SevereFor"/>.</summary>
    public const int BusyLoad = 90;

    /// <summary>A busy episode survives dips down to this; below it the clock starts over.</summary>
    public const int BusyRelaxedLoad = 80;

    /// <summary>A parked tree using at least this share of the machine's CPU or GPU is part of the load. Freezing the others would not help.</summary>
    public const int LoadSourceShare = 5;

    public static readonly TimeSpan MinParkedToTrim = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan RetrimInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SevereFor = TimeSpan.FromSeconds(30);

    /// <summary>"Freeze as soon as parked" waits this long, so a quick switch back and forth never freezes anything.</summary>
    public static readonly TimeSpan FreezeSettle = TimeSpan.FromSeconds(5);

    /// <summary>Below this the pill stays quiet - a few megabytes are not worth an announcement.</summary>
    public const long MinAnnouncedBytes = 50L * 1024 * 1024;

    private static readonly TimeSpan AudioCacheFor = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan PollWhileWatched = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollWhileParked = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollIdle = TimeSpan.FromSeconds(30);

    /// <summary>Processes that are never slowed down, whoever owns a window in them.</summary>
    private static readonly HashSet<string> NeverTouch = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ApplicationFrameHost", "dwm", "csrss", "winlogon", "wininit", "services", "lsass", "svchost",
        "smss", "System", "Idle", "Registry", "fontdrvhost", "sihost", "taskhostw", "RuntimeBroker", "audiodg",
        "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "TextInputHost", "ctfmon", "LockApp",
    };

    private sealed class PidState
    {
        public required long StartTicks { get; init; }
        public ResourceLevel Level { get; set; }
        public EcoMemento? Memento { get; init; }
        public DateTime LastTrim { get; set; }
        public bool FreezeFailed { get; set; }
    }

    private sealed class Group
    {
        public HashSet<int> Pids { get; } = [];
        public List<(Guid Category, OpenWindowInfo Window)> Windows { get; } = [];
        public bool Eligible { get; set; }
        public bool Playing { get; set; }

        /// <summary>Playing sound that the user's settings say must not be interrupted.</summary>
        public bool SoundProtected { get; set; }

        /// <summary>Would rest if it were silent: sound is the only thing keeping it awake.</summary>
        public bool HeldBySound { get; set; }
        public DateTime ParkedSince { get; set; }

        /// <summary>Share of the whole machine's CPU the tree used since the last look, 0-100.</summary>
        public double CpuPercent { get; set; }

        /// <summary>Share of the busiest graphics engine the tree used, 0-100.</summary>
        public int GpuPercent { get; set; }

        public bool IsLoadSource => CpuPercent >= LoadSourceShare || GpuPercent >= LoadSourceShare;
    }

    private readonly IWindowSessions _sessions;
    private readonly IProcessTable _table;
    private readonly IProcessGovernor _governor;
    private readonly ISystemMemory _memory;
    private readonly ISystemLoad? _load;
    private readonly IAudioActivity _audio;
    private readonly IThrottleLedger _ledger;
    private readonly IResourceSettings _settings;
    private readonly IClock _clock;
    private readonly int _ownProcessId;
    private readonly bool _autoRun;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, DateTime> _parkedSince = new();
    private readonly Dictionary<int, PidState> _state = new();
    private readonly Dictionary<int, long> _untouchable = new();
    private readonly Dictionary<int, DateTime> _awakeUntil = new();
    private readonly Dictionary<int, (TimeSpan Cpu, long StartTicks, DateTime At)> _previousCpu = new();
    private IReadOnlyList<ThrottleRecord> _lastSaved = [];
    private IReadOnlyList<CategoryResourceUsage> _usage = [];
    private string _usageSignature = "";
    private int _lastLoad;
    private bool _tight;
    private bool _announced;
    private DateTime? _severeSince;
    private DateTime? _busySince;
    private volatile bool _dirty;
    private bool _stopped;
    private IReadOnlySet<int> _playing = new HashSet<int>();
    private bool _audioReliable = true;
    private DateTime? _playingAt;
    private Timer? _timer;
    private bool _disposed;

    /// <param name="autoRun">Runs <see cref="Tick"/> on a timer and after state changes. Tests drive Tick by hand.</param>
    /// <param name="load">CPU and GPU load. Without it only memory pressure freezes "when the PC is under load".</param>
    public ResourceGovernor(IWindowSessions sessions, IProcessTable table, IProcessGovernor governor, ISystemMemory memory,
        IAudioActivity audio, IThrottleLedger ledger, IResourceSettings settings, IClock? clock = null,
        int? ownProcessId = null, bool autoRun = false, ISystemLoad? load = null)
    {
        _sessions = sessions;
        _table = table;
        _governor = governor;
        _memory = memory;
        _load = load;
        _audio = audio;
        _ledger = ledger;
        _settings = settings;
        _clock = clock ?? new SystemClock();
        _ownProcessId = ownProcessId ?? Environment.ProcessId;
        _autoRun = autoRun;
    }

    /// <summary>Levels or figures changed. Can fire on any thread.</summary>
    public event Action? Changed;

    /// <summary>A pass freed memory worth announcing. Can fire on any thread.</summary>
    public event Action<RelievedMemory>? Relieved;

    /// <summary>What each category costs as of the last look. Categories without windows are not listed.</summary>
    public IReadOnlyList<CategoryResourceUsage> Usage
    {
        get
        {
            lock (_gate) return _usage;
        }
    }

    /// <summary>Physical memory in use at the last look, 0-100.</summary>
    public int MemoryLoadPercent
    {
        get
        {
            lock (_gate) return _lastLoad;
        }
    }

    /// <summary>The home is open: figures are wanted every few seconds instead of only while something is parked.</summary>
    public bool UsageWanted { get; set; }

    /// <summary>
    /// Puts back whatever a crashed run left behind, then (with auto-run) starts following the sessions.
    /// Call once, at startup, after the window sessions exist.
    /// </summary>
    public void Initialize()
    {
        lock (_gate)
        {
            foreach (var record in _ledger.Load())
            {
                // A process that exited, or whose id someone else reuses now, is none of our business.
                if (_table.Measure(record.ProcessId) is not { } usage || usage.StartTicks != record.StartTicks) continue;
                if (record.Level == ResourceLevel.Frozen) _governor.Resume(record.ProcessId);
                if (record.Memento is { } memento) _governor.LeaveEco(record.ProcessId, memento);
            }
            // Whatever the ledger held is undone now: write it empty, whatever was cached.
            _lastSaved = [new ThrottleRecord(0, 0, ResourceLevel.Normal, null)];
            SaveLedger();
        }

        if (!_autoRun) return;
        _sessions.StateChanged += OnStateChanged;
        _timer = new Timer(_ => TimerElapsed(), null, Debounce, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Asks for a look soon (a state change, the home opening). Cheap; bursts collapse into one.</summary>
    public void RequestTick()
    {
        _dirty = true;
        if (_autoRun && !_disposed) _timer?.Change(Debounce, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// What the timer does each time it fires: a look if there is anything to do (something parked, something
    /// slowed down, or the home open), and how long to wait until the next one. A request made during the look
    /// asks again soon instead of being swallowed by the long poll. Public for tests.
    /// </summary>
    public TimeSpan Pulse()
    {
        // Cleared before the look, so anything requested while it runs is still pending afterwards.
        _dirty = false;
        var parked = HasParkedWindows();
        var resting = HasState();
        if (UsageWanted || parked || resting) Look(forceTrim: false);
        return _dirty ? Debounce : UsageWanted ? PollWhileWatched : parked || resting ? PollWhileParked : PollIdle;
    }

    /// <summary>Asked of the sessions each time, not remembered: a stale "nothing is parked" would never look again.</summary>
    private bool HasParkedWindows()
    {
        var active = _sessions.ActiveCategoryId;
        return _sessions.GetSessions().Any(s => s.Key != active && s.Value.Count > 0);
    }

    /// <summary>One look: memory pressure, process trees, what each tree should be now, and the figures. Public for tests.</summary>
    public void Tick() => Look(forceTrim: false);

    /// <summary>
    /// The manual "Free memory now": pages out every parked tree that may be touched, whatever the load and
    /// however briefly it has been parked. Null if there was nothing to page out or the setting is off.
    /// </summary>
    public RelievedMemory? FreeMemoryNow() => Look(forceTrim: true);

    /// <summary>
    /// Wakes these processes at once (resumes and speeds them up) and leaves them alone for <paramref name="hold"/>:
    /// a category being closed has to be able to answer. Afterwards, if they are still parked, they rest again.
    /// </summary>
    public void Wake(IEnumerable<int> processIds, TimeSpan hold)
    {
        lock (_gate)
        {
            var until = _clock.Now + hold;
            foreach (var pid in processIds) _awakeUntil[pid] = until;
        }
        Look(forceTrim: false);
    }

    /// <summary>
    /// Exit and crash handler: every process goes back to how it was, and the governor stops for good - a look
    /// that fired between this and the process ending would slow everything down again.
    /// </summary>
    public void ReleaseAll()
    {
        bool changed;
        lock (_gate)
        {
            _stopped = true;
            ReleaseAllLocked();
            changed = RecomputeSignature();
        }
        if (changed) Changed?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        _sessions.StateChanged -= OnStateChanged;
        _timer?.Dispose();
        _timer = null;
    }

    // ---------------- Timer ----------------

    private void OnStateChanged() => RequestTick();

    private void TimerElapsed()
    {
        var next = PollWhileParked;
        try
        {
            if (_disposed) return;
            next = Pulse();
        }
        catch (Exception)
        {
            // A failed look must not take the app down; the next one retries.
        }
        finally
        {
            if (!_disposed) _timer?.Change(next, Timeout.InfiniteTimeSpan);
        }
    }

    private bool HasState()
    {
        lock (_gate) return _state.Count > 0;
    }

    // ---------------- The look ----------------

    private RelievedMemory? Look(bool forceTrim)
    {
        RelievedMemory? announce = null;
        bool changed;
        lock (_gate)
        {
            if (_stopped) return null;
            var now = _clock.Now;
            var sessions = _sessions.GetSessions();
            var active = _sessions.ActiveCategoryId;
            var nodes = _table.Snapshot();
            var byPid = new Dictionary<int, ProcessNode>();
            foreach (var node in nodes) byPid.TryAdd(node.Pid, node);
            var children = nodes.Where(n => n.Pid != n.ParentPid).ToLookup(n => n.ParentPid);

            var cache = new Dictionary<int, ProcessUsage?>();
            ProcessUsage? UsageOf(int pid)
            {
                if (!cache.TryGetValue(pid, out var usage)) cache[pid] = usage = _table.Measure(pid);
                return usage;
            }

            UpdateParkedSince(sessions, active, now);
            UpdatePressure(now);

            var groups = BuildGroups(sessions, active, byPid, children, UsageOf);
            var activePids = sessions.TryGetValue(active, out var activeWindows)
                ? activeWindows.Select(w => w.ProcessId).ToHashSet()
                : [];
            // Only Catogarizer itself: the apps it launched from a template are its children, and they are
            // exactly what should rest when parked.
            var own = new HashSet<int> { _ownProcessId };

            var enabled = _settings.Enabled;
            var parkedGroups = groups.Where(g => g.Windows.Any(w => w.Category != active)).ToList();
            var playing = parkedGroups.Count > 0 && enabled ? PlayingProcesses(now) : new HashSet<int>();
            var audioReliable = _audioReliable;
            foreach (var pid in _awakeUntil.Where(p => p.Value <= now).Select(p => p.Key).ToList()) _awakeUntil.Remove(pid);
            foreach (var group in groups)
            {
                group.Playing = group.Pids.Overlaps(playing);
                group.SoundProtected = group.Playing && SoundProtects(group);
                var otherwiseEligible = enabled && IsEligible(group, active, activePids, own, byPid) && !group.Pids.Overlaps(_awakeUntil.Keys);
                group.Eligible = otherwiseEligible && !group.SoundProtected;
                group.HeldBySound = otherwiseEligible && group.SoundProtected;
                group.ParkedSince = group.Windows
                    .Where(w => _parkedSince.ContainsKey(w.Category))
                    .Select(w => _parkedSince[w.Category])
                    .DefaultIfEmpty(now).Max();
            }

            if (enabled)
            {
                var eligible = groups.Where(g => g.Eligible).ToList();
                Restore(eligible, UsageOf);
                EnterEco(eligible, UsageOf);
                SaveLedger();
                announce = Trim(eligible, forceTrim, now, active);
                UpdateLoad(eligible, now, UsageOf);
                Freeze(eligible, now, audioReliable);
            }
            else
            {
                _busySince = null;
                ReleaseAllLocked();
            }

            foreach (var pid in _untouchable.Keys.Where(p => !byPid.ContainsKey(p)).ToList()) _untouchable.Remove(pid);
            RecomputeUsage(groups, active, now, UsageOf);
            changed = RecomputeSignature();
        }

        if (changed) Changed?.Invoke();
        if (announce is not null) Relieved?.Invoke(announce);
        return announce;
    }

    /// <summary>The audio session list is a COM round trip; a few seconds old is fresh enough to know who is playing.</summary>
    private IReadOnlySet<int> PlayingProcesses(DateTime now)
    {
        if (_playingAt is { } at && now - at < AudioCacheFor) return _playing;
        _playing = _audio.PlayingProcessIds();
        _audioReliable = _audio.IsReliable;
        _playingAt = now;
        return _playing;
    }

    private void UpdateParkedSince(IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions, Guid active, DateTime now)
    {
        foreach (var (category, windows) in sessions)
            if (category != active && windows.Count > 0)
                _parkedSince.TryAdd(category, now);

        foreach (var category in _parkedSince.Keys.ToList())
            if (category == active || !sessions.TryGetValue(category, out var windows) || windows.Count == 0)
                _parkedSince.Remove(category);
    }

    private void UpdatePressure(DateTime now)
    {
        var load = _lastLoad = _memory.LoadPercent;
        if (!_tight)
        {
            if (load >= TightLoad)
            {
                _tight = true;
                _announced = false;
            }
        }
        else if (load <= CalmLoad)
        {
            _tight = false;
            _severeSince = null;
        }

        if (_tight && load >= SevereLoad) _severeSince ??= now;
        else if (load < TightLoad) _severeSince = null;
    }

    /// <summary>
    /// CPU and GPU pressure, tracked like the severe memory pressure: a busy episode starts at <see cref="BusyLoad"/>
    /// and is forgotten below <see cref="BusyRelaxedLoad"/>. Also measures what each eligible tree contributes.
    /// Only looks while some eligible tree freezes "when the PC is under load" - nobody else needs the figures.
    /// </summary>
    private void UpdateLoad(List<Group> eligible, DateTime now, Func<int, ProcessUsage?> usageOf)
    {
        if (_load is null || !eligible.Any(g => ModeOf(g) == FreezeMode.WhenTight))
        {
            _busySince = null;
            return;
        }

        var sample = _load.Sample();
        var busy = Math.Max(sample.CpuPercent, sample.GpuPercent);
        if (busy >= BusyLoad) _busySince ??= now;
        else if (busy < BusyRelaxedLoad) _busySince = null;

        var cores = Math.Max(1, Environment.ProcessorCount);
        foreach (var group in eligible)
        {
            group.CpuPercent = group.Pids.Sum(pid => usageOf(pid) is { } usage ? CpuPercentOf(pid, usage, now, cores) : 0);
            group.GpuPercent = Math.Min(100, group.Pids.Sum(pid => sample.GpuByProcess.GetValueOrDefault(pid)));
        }
    }

    // ---------------- Process trees ----------------

    /// <summary>The process and everything below it. A child that started before its parent is a reused id, not a child.</summary>
    private static HashSet<int> Descend(int root, ILookup<int, ProcessNode> children, Func<int, ProcessUsage?> usageOf)
    {
        var tree = new HashSet<int> { root };
        var stack = new Stack<int>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var parent = stack.Pop();
            var parentStart = usageOf(parent)?.StartTicks;
            foreach (var child in children[parent])
            {
                if (tree.Contains(child.Pid)) continue;
                if (parentStart is { } p && usageOf(child.Pid) is { } c && c.StartTicks < p) continue;
                tree.Add(child.Pid);
                stack.Push(child.Pid);
            }
        }
        return tree;
    }

    private List<Group> BuildGroups(IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions, Guid active,
        Dictionary<int, ProcessNode> byPid, ILookup<int, ProcessNode> children, Func<int, ProcessUsage?> usageOf)
    {
        var windows = sessions.SelectMany(s => s.Value.Select(w => (Category: s.Key, Window: w)))
            .Where(x => byPid.ContainsKey(x.Window.ProcessId))
            .ToList();

        var trees = windows.Select(x => x.Window.ProcessId).Distinct()
            .ToDictionary(pid => pid, pid => Descend(pid, children, usageOf));

        // Larger trees first, so a window owned by a helper process joins the group of the process above it.
        var groups = new List<Group>();
        foreach (var (root, tree) in trees.OrderByDescending(t => t.Value.Count))
        {
            var group = groups.FirstOrDefault(g => g.Pids.Contains(root));
            if (group is null)
            {
                group = new Group();
                groups.Add(group);
            }
            group.Pids.UnionWith(tree);
            group.Windows.AddRange(windows.Where(x => x.Window.ProcessId == root));
        }
        return groups;
    }

    private bool IsEligible(Group group, Guid active, HashSet<int> activePids, HashSet<int> own, Dictionary<int, ProcessNode> byPid)
    {
        if (group.Windows.Any(w => w.Category == active)) return false;
        if (group.Pids.Overlaps(activePids) || group.Pids.Overlaps(own)) return false;
        foreach (var pid in group.Pids)
        {
            if (!byPid.TryGetValue(pid, out var node)) continue;
            if (NeverTouch.Contains(node.Name) || _settings.IsPinned(node.Name)) return false;
        }
        return true;
    }

    /// <summary>
    /// Whether sound makes this group untouchable. The most careful window decides, like <see cref="ModeOf"/>: one
    /// window whose category keeps sound awake protects the whole tree - except an app the user explicitly allowed
    /// to freeze, which has said it does not mind (otherwise "allowed" would silently never happen for a game).
    /// </summary>
    private bool SoundProtects(Group group) =>
        group.Windows.Any(w => _settings.KeepsSoundAwake(w.Category) && !_settings.IsAppFreezable(w.Window.ProcessName));

    // ---------------- Levels ----------------

    private void Restore(List<Group> eligible, Func<int, ProcessUsage?> usageOf)
    {
        var keep = eligible.SelectMany(g => g.Pids).ToHashSet();
        foreach (var (pid, state) in _state.ToList())
        {
            if (keep.Contains(pid)) continue;
            Release(pid, state, usageOf(pid));
        }
    }

    private void Release(int pid, PidState state, ProcessUsage? usage)
    {
        _state.Remove(pid);
        // Gone, or its id reused by a stranger: nothing to put back.
        if (usage is not { } u || u.StartTicks != state.StartTicks) return;
        if (state.Level == ResourceLevel.Frozen) _governor.Resume(pid);
        if (state.Memento is { } memento) _governor.LeaveEco(pid, memento);
    }

    private void ReleaseAllLocked()
    {
        foreach (var (pid, state) in _state.ToList())
            Release(pid, state, _table.Measure(pid));
        _untouchable.Clear();
        SaveLedger();
    }

    private void EnterEco(List<Group> eligible, Func<int, ProcessUsage?> usageOf)
    {
        foreach (var pid in eligible.SelectMany(g => g.Pids))
        {
            if (_state.ContainsKey(pid)) continue;

            var usage = usageOf(pid);
            var start = usage?.StartTicks ?? -1;
            if (usage is null || (_untouchable.TryGetValue(pid, out var known) && known == start))
            {
                _untouchable[pid] = start;
                continue;
            }

            if (_governor.EnterEco(pid) is { } memento)
                _state[pid] = new PidState { StartTicks = start, Level = ResourceLevel.Eco, Memento = memento };
            else
                _untouchable[pid] = start;
        }
    }

    /// <summary>Pages out parked trees under pressure (or on demand). Returns what is worth announcing, if anything.</summary>
    private RelievedMemory? Trim(List<Group> eligible, bool forceTrim, DateTime now, Guid active)
    {
        if (!forceTrim && !_tight) return null;

        long freed = 0;
        var trimmed = new List<Guid>();
        var candidates = eligible
            .Where(g => forceTrim || now - g.ParkedSince >= MinParkedToTrim)
            .OrderBy(g => g.ParkedSince);
        foreach (var group in candidates)
        {
            var states = group.Pids.Select(p => (Pid: p, State: _state.GetValueOrDefault(p))).Where(x => x.State is not null).ToList();
            if (states.Count == 0) continue;

            var due = forceTrim
                || states.Any(x => x.State!.Level < ResourceLevel.Asleep)
                || states.All(x => x.State!.Level == ResourceLevel.Asleep && now - x.State.LastTrim >= RetrimInterval);
            if (!due) continue;

            var targets = states.Where(x => x.State!.Level <= ResourceLevel.Asleep).ToList();
            if (targets.Count == 0) continue;

            var before = targets.Sum(x => _table.Measure(x.Pid)?.PrivateBytes ?? 0);
            foreach (var (pid, state) in targets)
            {
                _governor.Trim(pid);
                state!.Level = ResourceLevel.Asleep;
                state.LastTrim = now;
            }
            var after = targets.Sum(x => _table.Measure(x.Pid)?.PrivateBytes ?? 0);
            freed += Math.Max(0, before - after);
            trimmed.AddRange(group.Windows.Select(w => w.Category).Where(c => c != active));

            if (!forceTrim && _memory.LoadPercent < RelievedLoad) break;
        }

        if (freed <= 0) return null;
        if (forceTrim) return new RelievedMemory(freed, trimmed.Distinct().ToList());
        if (_announced || freed < MinAnnouncedBytes) return null;
        _announced = true;
        return new RelievedMemory(freed, trimmed.Distinct().ToList());
    }

    private void Freeze(List<Group> eligible, DateTime now, bool audioReliable)
    {
        foreach (var group in eligible)
        {
            var mode = ModeOf(group);
            // Unreadable audio only matters where sound would have protected the group; for the rest nothing is at risk.
            var allowed = mode != FreezeMode.Never && (audioReliable || !SoundProtects(group));
            var states = group.Pids.Select(p => (Pid: p, State: _state.GetValueOrDefault(p))).Where(x => x.State is not null).ToList();

            if (!allowed)
            {
                // The user withdrew the permission (or sound can no longer be ruled out): wake it up.
                foreach (var (pid, state) in states.Where(x => x.State!.Level == ResourceLevel.Frozen))
                {
                    _governor.Resume(pid);
                    state!.Level = ResourceLevel.Asleep;
                }
                continue;
            }

            var parkedFor = now - group.ParkedSince;
            var wanted = mode == FreezeMode.Immediately
                ? parkedFor >= FreezeSettle
                : (MemorySevere(now) && parkedFor >= MinParkedToTrim) || (group.IsLoadSource && LoadSustained(now) && parkedFor >= FreezeSettle);
            if (!wanted) continue;

            var targets = states.Where(x => x.State!.Level < ResourceLevel.Frozen && !x.State.FreezeFailed).ToList();
            if (targets.Count == 0) continue;

            // Named in the ledger first: a crash between here and the end must never leave one frozen.
            var previous = targets.ToDictionary(x => x.Pid, x => x.State!.Level);
            foreach (var (_, state) in targets) state!.Level = ResourceLevel.Frozen;
            SaveLedger();
            foreach (var (pid, state) in targets)
            {
                if (_governor.Suspend(pid)) continue;
                state!.Level = previous[pid];
                state.FreezeFailed = true;
            }
            SaveLedger();
        }
    }

    /// <summary>Paging out was not enough: memory has stayed severe for a while.</summary>
    private bool MemorySevere(DateTime now) => _severeSince is { } since && now - since >= SevereFor;

    /// <summary>The processor or the graphics card has been maxed out for a while.</summary>
    private bool LoadSustained(DateTime now) => _busySince is { } since && now - since >= SevereFor;

    /// <summary>The most careful permission among the group's windows: one window that may not be frozen decides for all.</summary>
    private FreezeMode ModeOf(Group group)
    {
        var mode = FreezeMode.Immediately;
        foreach (var (category, window) in group.Windows)
        {
            var own = _settings.FreezeModeOf(category);
            if (own == FreezeMode.Never && _settings.IsAppFreezable(window.ProcessName)) own = FreezeMode.WhenTight;
            if (own < mode) mode = own;
        }
        return mode;
    }

    // ---------------- Figures ----------------

    private void RecomputeUsage(List<Group> groups, Guid active, DateTime now, Func<int, ProcessUsage?> usageOf)
    {
        var cores = Math.Max(1, Environment.ProcessorCount);
        var perCategory = new Dictionary<Guid, (long Bytes, double Cpu, int Untouchable, List<(ResourceLevel Level, long Bytes)> Trees, bool Held)>();
        var cpuNow = new Dictionary<int, (TimeSpan Cpu, long StartTicks, DateTime At)>();

        foreach (var group in groups)
        {
            long bytes = 0;
            double cpu = 0;
            var untouchable = 0;
            var levels = new List<ResourceLevel>();
            foreach (var pid in group.Pids)
            {
                var usage = usageOf(pid);
                if (usage is { } u)
                {
                    bytes += u.PrivateBytes;
                    cpuNow[pid] = (u.CpuTime, u.StartTicks, now);
                    cpu += CpuPercentOf(pid, u, now, cores);
                }

                var start = usage?.StartTicks ?? -1;
                if (_untouchable.TryGetValue(pid, out var known) && known == start) untouchable++;
                else levels.Add(_state.TryGetValue(pid, out var state) && group.Eligible ? state.Level : ResourceLevel.Normal);
            }

            var level = group.Eligible && levels.Count > 0 ? levels.Min() : ResourceLevel.Normal;
            var owner = OwnerOf(group, active);
            if (!perCategory.TryGetValue(owner, out var sum)) sum = (0, 0, 0, [], false);
            sum.Trees.Add((level, bytes));
            perCategory[owner] = (sum.Bytes + bytes, sum.Cpu + cpu, sum.Untouchable + untouchable, sum.Trees, sum.Held || group.HeldBySound);
        }

        _previousCpu.Clear();
        foreach (var (pid, value) in cpuNow) _previousCpu[pid] = value;
        _usage = perCategory.Select(p => new CategoryResourceUsage(p.Key, p.Value.Bytes, p.Value.Cpu, MajorityLevel(p.Value.Trees), p.Value.Untouchable, p.Value.Held)).ToList();
    }

    /// <summary>Share of the whole machine's CPU this process used since the previous look; 0 when there was none.</summary>
    private double CpuPercentOf(int pid, ProcessUsage usage, DateTime now, int cores) =>
        _previousCpu.TryGetValue(pid, out var before) && before.StartTicks == usage.StartTicks && now > before.At
            ? (usage.CpuTime - before.Cpu).TotalSeconds / ((now - before.At).TotalSeconds * cores) * 100
            : 0;

    /// <summary>
    /// What most of the category's memory is doing: the highest level that at least half of its bytes have reached.
    /// One tree that must stay awake (a game playing sound) does not hide that everything else is resting.
    /// </summary>
    private static ResourceLevel MajorityLevel(List<(ResourceLevel Level, long Bytes)> trees)
    {
        var total = trees.Sum(t => t.Bytes);
        if (total <= 0) return trees.Count == 0 ? ResourceLevel.Normal : trees.Min(t => t.Level);

        long reached = 0;
        foreach (var tree in trees.OrderByDescending(t => t.Level))
        {
            reached += tree.Bytes;
            if (reached * 2 >= total) return tree.Level;
        }
        return ResourceLevel.Normal;
    }

    /// <summary>
    /// A tree shared by windows of several categories is counted once, for the category with most windows in it
    /// (the active one on a tie, then the one parked longest), so the figures add up to what the machine really uses.
    /// </summary>
    private Guid OwnerOf(Group group, Guid active) =>
        group.Windows.GroupBy(w => w.Category)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key == active)
            .ThenBy(g => _parkedSince.GetValueOrDefault(g.Key, DateTime.MaxValue))
            .ThenBy(g => g.Key)
            .First().Key;

    /// <summary>True if what the home shows (rounded, so a few KB of jitter is not a change) differs from the last look.</summary>
    private bool RecomputeSignature()
    {
        var signature = string.Join(";", _usage.OrderBy(u => u.CategoryId)
            .Select(u => $"{u.CategoryId:N}:{u.PrivateBytes / (10L * 1024 * 1024)}:{(int)Math.Round(u.CpuPercent)}:{(int)u.Level}:{u.UntouchableProcesses}:{(u.HeldAwakeBySound ? 1 : 0)}"));
        if (signature == _usageSignature) return false;
        _usageSignature = signature;
        return true;
    }

    // ---------------- Ledger ----------------

    private void SaveLedger()
    {
        var records = _state
            .Where(p => p.Value.Level >= ResourceLevel.Eco)
            .OrderBy(p => p.Key)
            .Select(p => new ThrottleRecord(p.Key, p.Value.StartTicks, p.Value.Level, p.Value.Memento))
            .ToList();
        if (records.SequenceEqual(_lastSaved)) return;

        try
        {
            _ledger.Save(records);
            _lastSaved = records;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Throttling keeps working; the in-process ReleaseAll on exit still covers the normal path,
            // only crash recovery loses this one update.
        }
    }
}
