using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using Catogarizer.App.Services;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Resources;
using Catogarizer.Core.Services;
using Catogarizer.Core.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

/// <summary>
/// The "Now + Shelf" home (PRINCIPLES.md, value 5): where am I, what's parked where, what's
/// held back - at a glance. Editing lives in the category editor, not here.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int MaxWindowRows = 6;
    private const int MaxShelfIcons = 3;

    private readonly EditorServices _services;
    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly ResourceGovernor _resources;
    private readonly ThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly Action<string> _onHotkeyChanged;
    private readonly UpdateNotifier _updateNotifier;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refreshDebounce;
    private (string Name, DateTime At, Guid CategoryId)? _lastBlocked;
    private Guid? _noticeCategoryId;

    // ---- Now ----
    [ObservableProperty] private string _activeName = "";
    [ObservableProperty] private bool _activeIsUnsorted;
    [ObservableProperty] private bool _activeIsTemporary;
    /// <summary>The active category can be closed: everything but Unsorted.</summary>
    [ObservableProperty] private bool _canCloseActive;
    /// <summary>The active category has an editor: not Unsorted, not a quick category.</summary>
    [ObservableProperty] private bool _canEditActive;
    [ObservableProperty] private Brush _activeColor = Brushes.Gray;
    [ObservableProperty] private Brush _heroTint = Brushes.Transparent;
    [ObservableProperty] private string _activeSinceText = "";
    [ObservableProperty] private string _windowCountText = "";
    [ObservableProperty] private string? _moreWindowsText;
    [ObservableProperty] private string? _noWindowsText;
    [ObservableProperty] private string? _heldBackEmptyText;
    [ObservableProperty] private string? _lastBlockedText;

    public ObservableCollection<WindowRow> ActiveWindows { get; } = new();

    /// <summary>Programs that kept a window open after a close, each with its own way to show or end it.</summary>
    public ObservableCollection<StubbornRow> StubbornRows { get; } = new();
    /// <summary>Where a window of the active category can be sent: every other category, plus Unsorted.</summary>
    public ObservableCollection<MoveTarget> MoveTargets { get; } = new();
    public ObservableCollection<HeldBackRow> HeldBack { get; } = new();
    public ObservableCollection<PinnedRow> Pinned { get; } = new();

    // ---- Back ----
    [ObservableProperty] private bool _hasPrevious;
    [ObservableProperty] private string _backTitle = "";
    [ObservableProperty] private string _backDetail = "";
    [ObservableProperty] private string _backHint = "";
    [ObservableProperty] private bool _showAllWindows;
    [ObservableProperty] private bool _noticeCanEdit;
    [ObservableProperty] private Brush _backColor = Brushes.Gray;

    // ---- Shelf ----
    public ObservableCollection<ShelfItem> Shelf { get; } = new();
    [ObservableProperty] private bool _hasCategories;

    // ---- Resources ----
    public ObservableCollection<ResourceRow> ResourceRows { get; } = new();
    [ObservableProperty] private string _memoryLoadText = "";
    [ObservableProperty] private string? _parkedBytesText;

    /// <summary>The one calm line under the Resources rows: off, nothing parked, or what could not be slowed down.</summary>
    [ObservableProperty] private string? _resourcesNote;

    /// <summary>"Free memory now" is offered while something is parked and the setting is on.</summary>
    [ObservableProperty] private bool _canFreeMemory;

    /// <summary>The answer to "Free memory now" when there was nothing to free; gone with the next switch.</summary>
    [ObservableProperty] private string? _freeNowText;
    private Guid? _freeNowCategoryId;

    // ---- Closing a category ----
    [ObservableProperty] private bool _isClosing;

    /// <summary>The calm line after a close: "Comms closed - 5 windows." or what stayed open. Gone with the next switch.</summary>
    [ObservableProperty] private string? _closeReportText;

    private CloseReport? _closeReport;
    private Guid? _closeReportActiveId;

    /// <summary>The banner goes away, and its rows with it.</summary>
    partial void OnCloseReportTextChanged(string? value)
    {
        if (value is null) StubbornRows.Clear();
    }

    // ---- Footer / chrome ----
    [ObservableProperty] private string? _nextTriggerText;
    [ObservableProperty] private IReadOnlyList<string> _hotkeyKeys = [];
    [ObservableProperty] private string? _notice;

    /// <summary>"Version 1.2.0 is available.", or null. A quiet note with a link, never a dialog.</summary>
    [ObservableProperty] private string? _updateText;

    public MainViewModel(EditorServices services, CategorySwitcher switcher, ThemeService themeService,
        IAppBlockingService appBlockingService, UpdateNotifier updateNotifier, Action<string> onHotkeyChanged)
    {
        _services = services;
        _library = services.Library;
        _dialogService = services.Dialogs;
        _switcher = switcher;
        _resources = services.Resources;
        _themeService = themeService;
        _onHotkeyChanged = onHotkeyChanged;
        _updateNotifier = updateNotifier;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _refreshDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) =>
        {
            _refreshDebounce!.Stop();
            Refresh();
        }, _dispatcher);
        _refreshDebounce.Stop();

        switcher.StateChanged += RequestRefresh;
        _resources.Changed += RequestRefresh;
        themeService.ThemeChanged += RequestRefresh;
        updateNotifier.Changed += RequestRefresh;
        appBlockingService.AppBlocked += name =>
        {
            _lastBlocked = (name, DateTime.Now, _switcher.ActiveCategoryId);
            RequestRefresh();
        };

        Refresh();
    }

    /// <summary>
    /// Set by the view. While the home sits hidden in the tray, events don't refresh it - windows
    /// appear all day - and showing it refreshes once instead.
    /// </summary>
    public bool IsViewVisible
    {
        get => _isViewVisible;
        set
        {
            _isViewVisible = value;
            // Figures are only measured every few seconds while someone looks at them.
            _resources.UsageWanted = value;
            if (value) _resources.RequestTick();
        }
    }

    private bool _isViewVisible;

    /// <summary>Safe from any thread; bursts of events collapse into one refresh.</summary>
    public void RequestRefresh() => _dispatcher.BeginInvoke(() =>
    {
        if (!IsViewVisible) return;
        _refreshDebounce.Stop();
        _refreshDebounce.Start();
    });

    public void Refresh()
    {
        var sessions = _switcher.GetSessions();
        var activeId = _switcher.ActiveCategoryId;
        var categories = _library.Categories.OrderBy(c => c.SortOrder).ToList();
        var active = categories.FirstOrDefault(c => c.Id == activeId);

        // Quick categories don't count: with only one of those, the first-run card still has a job.
        HasCategories = categories.Any(c => !c.IsTemporary);
        HotkeyKeys = _library.Settings.CommandPaletteHotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        RefreshNow(active, sessions);
        RefreshBack(categories, sessions);
        RefreshShelf(categories, activeId, sessions);
        RefreshResources(categories, activeId, sessions);
        RefreshPinned();
        RefreshNextTrigger(categories);
        UpdateText = _updateNotifier.Visible is { } update ? $"Version {update.Latest} is available." : null;
    }

    private void RefreshNow(Category? active, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var activeId = _switcher.ActiveCategoryId;
        ActiveIsUnsorted = active is null;
        ActiveIsTemporary = active is { IsTemporary: true };
        CanCloseActive = active is not null;
        CanEditActive = active is { IsTemporary: false };
        ActiveName = _library.NameOf(activeId);
        var color = _themeService.ColorFor(active);
        ActiveColor = ThemeService.FrozenBrush(color);
        // Unsorted has no color of its own; a gray tint would only muddy the theme.
        HeroTint = active is null ? Brushes.Transparent : HeroGradient(color);

        var since = _switcher.ActiveSince;
        var elapsed = DateTime.Now - since;
        ActiveSinceText = elapsed.TotalMinutes < 1 ? $"since {since:HH:mm}" : $"since {since:HH:mm} · {Duration(elapsed)}";

        var targets = _library.Categories.OrderBy(c => c.SortOrder).Where(c => c.Id != activeId)
            .Select(c => new MoveTarget(c.Id, c.Name, _themeService.ColorFor(c))).ToList();
        if (activeId != CategorySwitchService.Uncategorized)
            targets.Add(new MoveTarget(CategorySwitchService.Uncategorized, CategorySwitchService.UncategorizedName, _themeService.ColorFor(null)));
        Replace(MoveTargets, targets);

        var windows = sessions.TryGetValue(activeId, out var list) ? list : [];
        var opening = _switcher.IsOpeningApps && active is not null;
        WindowCountText = opening ? "opening apps..." : windows.Count switch { 0 => "No windows", 1 => "1 window", var n => $"{n} windows" };
        var shown = ShowAllWindows ? windows.Count : MaxWindowRows;
        Replace(ActiveWindows, windows.Take(shown).Select(w => new WindowRow(w.Title, AppNames.ForProcess(w.ProcessId, w.ProcessName), w.ProcessId, w.Handle, w.ProcessName)));
        MoreWindowsText = windows.Count <= MaxWindowRows ? null
            : ShowAllWindows ? "Show fewer"
            : $"Show {windows.Count - MaxWindowRows} more";
        NoWindowsText = windows.Count > 0 ? null
            : opening ? $"Opening {string.Join(", ", _library.AppsOf(active!).Select(a => a.Name))}..."
            : active is null ? "Nothing open outside your categories."
            : active.IsTemporary ? "Windows you open now join this category. It disappears when the last one closes."
            : "Windows you open now join this category.";

        // A launch problem belongs to the switch that caused it; the next switch clears it.
        if (Notice is not null && _noticeCategoryId != activeId) Notice = null;
        if (CloseReportText is not null && _closeReportActiveId != activeId) CloseReportText = null;

        var blocked = active is null ? [] : _library.BlockedAppsOf(active);
        Replace(HeldBack, blocked.Select(b => new HeldBackRow(b.Name, PathOrNull(b.ProcessNameOrPath), RunningProcessOf(b.ProcessNameOrPath))));
        HeldBackEmptyText = blocked.Count > 0 ? null
            : active is null ? "Unsorted never holds anything back."
            : active.IsTemporary ? "Quick categories don't hold anything back."
            : "Nothing is held back here. Add distracting apps in the editor.";
        // Only for this visit: coming back later, an old attempt would read like a new one.
        LastBlockedText = _lastBlocked is { } last && last.CategoryId == activeId && last.At >= _switcher.ActiveSince
            ? $"{last.Name} tried to open at {last.At:HH:mm} and was closed."
            : null;
    }

    private void RefreshBack(List<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var previousId = _switcher.PreviousCategoryId;
        var previous = categories.FirstOrDefault(c => c.Id == previousId);
        HasPrevious = previousId is not null && (previous is not null || previousId == CategorySwitchService.Uncategorized);
        if (!HasPrevious) return;

        BackTitle = $"Back to {_library.NameOf(previousId!.Value)}";
        BackHint = $"or press {_library.Settings.CommandPaletteHotkey} twice";
        var parked = sessions.TryGetValue(previousId.Value, out var w) ? w.Count : 0;
        var missing = parked > 0 || previous is null ? 0 : _library.MissingAppCount(previous);
        var template = previous is null ? [] : _library.AppsOf(previous);
        BackDetail = CategoryStateText.Describe(parked, missing, template.Count, previous is null,
            parked > 0 ? 0 : CategoryStateText.OpenInUnsorted(template, sessions));
        BackColor = ThemeService.FrozenBrush(_themeService.ColorFor(previous));
    }

    private void RefreshShelf(List<Category> categories, Guid activeId, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var items = new List<ShelfItem>();
        foreach (var category in categories.Where(c => c.Id != activeId))
        {
            var apps = _library.AppsOf(category);
            var parked = sessions.TryGetValue(category.Id, out var w) ? w.Count : 0;
            var missing = parked > 0 ? 0 : _library.MissingAppCount(category);
            items.Add(new ShelfItem
            {
                Id = category.Id,
                Name = category.Name,
                KeyText = _library.KeyOf(category.Id),
                Color = ThemeService.FrozenBrush(_themeService.ColorFor(category)),
                IsUnsorted = false,
                IsTemporary = category.IsTemporary,
                Icons = apps.Take(MaxShelfIcons).Select(a => new TemplateIcon(a.Name, a.ExecutablePath)).ToList(),
                MoreIconsText = apps.Count > MaxShelfIcons ? $"+{apps.Count - MaxShelfIcons}" : null,
                StateText = CategoryStateText.Describe(parked, missing, apps.Count, isUnsorted: false,
                    openInUnsorted: parked > 0 ? 0 : CategoryStateText.OpenInUnsorted(apps, sessions)),
                HasParkedWindows = parked > 0,
                BlockedCount = category.BlockedAppIds.Count,
                TemplateCount = apps.Count,
                HasMissingApp = missing > 0,
            });
        }

        if (activeId != CategorySwitchService.Uncategorized)
        {
            var parkedWindows = sessions.TryGetValue(CategorySwitchService.Uncategorized, out var w) ? w : [];
            var parked = parkedWindows.Count;
            // Unsorted has no template, so it shows what's parked there instead.
            var parkedApps = parkedWindows
                .Select(pw => (Window: pw, Path: AppNames.ExecutableOf(pw.ProcessId)))
                .Where(x => x.Path is not null)
                .DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            items.Add(new ShelfItem
            {
                Id = CategorySwitchService.Uncategorized,
                Name = CategorySwitchService.UncategorizedName,
                KeyText = _library.KeyOf(CategorySwitchService.Uncategorized),
                Color = ThemeService.FrozenBrush(_themeService.ColorFor(null)),
                IsUnsorted = true,
                IsTemporary = false,
                Icons = parkedApps.Take(MaxShelfIcons).Select(x => new TemplateIcon(AppNames.ForProcess(x.Window.ProcessId, x.Window.ProcessName), x.Path!)).ToList(),
                MoreIconsText = parkedApps.Count > MaxShelfIcons ? $"+{parkedApps.Count - MaxShelfIcons}" : null,
                StateText = CategoryStateText.Describe(parked, 0, 0, isUnsorted: true),
                HasParkedWindows = parked > 0,
                BlockedCount = 0,
                TemplateCount = 0,
                HasMissingApp = false,
            });
        }

        var usage = _resources.Usage;
        foreach (var item in items)
        {
            var costs = usage.FirstOrDefault(u => u.CategoryId == item.Id);
            item.Level = costs?.Level ?? ResourceLevel.Normal;
            item.MemoryBytes = costs?.PrivateBytes ?? 0;
            item.HoldsAwakeBySound = costs?.HeldAwakeBySound ?? false;
        }

        // Same tiles as before: update only what they cost, in place, so a tile can animate falling asleep
        // (a rebuilt tile would start over, and the shelf would flicker every few seconds).
        if (Shelf.Count == items.Count && Shelf.Zip(items).All(pair => pair.First.SameEntryAs(pair.Second)))
        {
            foreach (var (shown, fresh) in Shelf.Zip(items))
            {
                shown.Level = fresh.Level;
                shown.MemoryBytes = fresh.MemoryBytes;
                shown.HoldsAwakeBySound = fresh.HoldsAwakeBySound;
            }
            return;
        }

        // Keep the item a switch is running on, so its "Opening..." state survives refreshes.
        var switching = Shelf.FirstOrDefault(s => s.IsSwitching)?.Id;
        Replace(Shelf, items);
        if (switching is { } id && Shelf.FirstOrDefault(s => s.Id == id) is { } still) still.IsSwitching = true;
    }

    private void RefreshResources(List<Category> categories, Guid activeId, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var usage = _resources.Usage;
        var total = usage.Sum(u => u.PrivateBytes);
        Replace(ResourceRows, usage.OrderByDescending(u => u.PrivateBytes).Select(u =>
        {
            var category = categories.FirstOrDefault(c => c.Id == u.CategoryId);
            var state = u.CategoryId == activeId ? "Here" : u.Level switch
            {
                ResourceLevel.Eco => "Eco",
                ResourceLevel.Asleep => "Asleep",
                ResourceLevel.Frozen => "Frozen",
                _ => "Awake",
            };
            return new ResourceRow(_library.NameOf(u.CategoryId), _themeService.ColorFor(category), state, u.Level,
                ByteSize.Format(u.PrivateBytes), $"{Math.Round(u.CpuPercent)} %", total > 0 ? u.PrivateBytes * 100.0 / total : 0,
                u.HeldAwakeBySound && u.CategoryId != activeId);
        }));

        var parked = usage.Where(u => u.CategoryId != activeId).ToList();
        var parkedBytes = parked.Sum(u => u.PrivateBytes);
        ParkedBytesText = parkedBytes > 0 ? $"Parked: {ByteSize.Format(parkedBytes)}" : null;
        MemoryLoadText = _resources.MemoryLoadPercent > 0 ? $"Memory {_resources.MemoryLoadPercent} % used" : "";

        var untouchable = parked.Sum(u => u.UntouchableProcesses);
        var anythingParked = sessions.Any(s => s.Key != activeId && s.Value.Count > 0);
        CanFreeMemory = anythingParked && _library.Settings.SaveResources;
        if (FreeNowText is not null && _freeNowCategoryId != activeId) FreeNowText = null;
        ResourcesNote = !_library.Settings.SaveResources ? "Resource saving is off. Turn it on in Settings."
            : !anythingParked ? "Nothing is parked, so nothing is slowed down. Park a category by switching away from it."
            : untouchable > 0 ? $"{untouchable} {(untouchable == 1 ? "process runs" : "processes run")} as administrator or are protected and stay at full speed. Catogarizer never asks for admin rights."
            : null;
    }

    private void RefreshPinned() =>
        Replace(Pinned, _library.PinnedApps.Select(p => new PinnedRow(p.Id, p.Name, PathOrNull(p.ProcessNameOrPath), RunningProcessOf(p.ProcessNameOrPath))));

    private static string? PathOrNull(string value) => ProcessPattern.IsPath(value) ? value : null;

    /// <summary>For bare process names ("claude"): a running process to take the icon from, or 0.</summary>
    private static int RunningProcessOf(string value)
    {
        if (ProcessPattern.IsPath(value)) return 0;
        var processes = System.Diagnostics.Process.GetProcessesByName(ProcessPattern.ProcessName(value));
        try
        {
            return processes.FirstOrDefault()?.Id ?? 0;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private void RefreshNextTrigger(List<Category> categories)
    {
        var next = TriggerPreview.Next(_library.Triggers, DateTime.Now);
        if (next is null)
        {
            NextTriggerText = null;
            return;
        }
        var target = next.Trigger.Actions
            .Where(a => a.Type == TriggerActionType.OpenCategory)
            .Select(a => categories.FirstOrDefault(c => c.Id == a.CategoryId)?.Name)
            .FirstOrDefault(n => n is not null);
        var when = next.At.Date == DateTime.Today ? $"at {next.At:HH:mm}" : $"tomorrow at {next.At:HH:mm}";
        NextTriggerText = target is null
            ? $"“{next.Trigger.Name}” runs {when}"
            : $"“{next.Trigger.Name}” switches to {target} {when}";
    }

    // ---------------- Commands ----------------

    [RelayCommand]
    private async Task SwitchToAsync(ShelfItem item)
    {
        Notice = null;
        // A restore is instant; only a fresh template launch gets the "Opening..." state.
        item.IsSwitching = !item.HasParkedWindows && !item.IsUnsorted && item.TemplateCount > 0;
        try
        {
            var result = await Task.Run(() => _switcher.SwitchTo(item.Id));
            ShowFailures(result);
        }
        finally
        {
            item.IsSwitching = false;
        }
    }

    [RelayCommand]
    private async Task SwitchBackAsync()
    {
        Notice = null;
        ShowFailures(await Task.Run(_switcher.SwitchBack));
    }

    /// <summary>Number keys on the home: 1-9 are categories in order, 0 is Unsorted.</summary>
    public async Task SwitchByNumberAsync(int number)
    {
        if (_library.CategoryIdForKey(number) is not { } id) return;
        Notice = null;
        ShowFailures(await Task.Run(() => _switcher.SwitchTo(id)));
    }

    [RelayCommand]
    private void NewCategory()
    {
        var name = _dialogService.ShowCategoryEdit(new CategoryEditDialogViewModel(_library.Categories));
        if (name is null) return;
        var category = _library.AddCategory(name);
        Refresh();
        OpenEditor(category.Id, suggestOpenApps: true);
    }

    /// <summary>Home button, <c>+</c> key: a category for the next hour that removes itself (CONCEPT.md "Temporary categories").</summary>
    [RelayCommand]
    public async Task NewQuickCategoryAsync()
    {
        Notice = null;
        await _services.QuickCategory.StartAsync();
        Refresh();
    }

    /// <summary>Hero button, Ctrl+W: closes the active category - its windows are asked to close, nothing is killed.</summary>
    [RelayCommand]
    public Task CloseActiveAsync() => CloseCategoryAsync(_switcher.ActiveCategoryId);

    /// <summary>Right-click a tile on the shelf: closes that parked category without switching to it.</summary>
    [RelayCommand]
    public Task CloseShelfCategoryAsync(ShelfItem item) => CloseCategoryAsync(item.Id);

    private async Task CloseCategoryAsync(Guid id)
    {
        if (!CategoryCloseService.CanClose(id)) return;

        var windows = _switcher.GetSessions().TryGetValue(id, out var list) ? list : [];
        if (windows.Count > 0)
        {
            var confirmed = _dialogService.ShowConfirm($"Close \"{_library.NameOf(id)}\"?",
                (windows.Count == 1 ? "Its window will be asked to close." : $"Its {windows.Count} windows will be asked to close.")
                + " Apps with unsaved work will ask you first. The programs keep running; a chat app may stay in the tray.",
                "Close windows",
                windows.Select(WindowLabel).ToList());
            if (!confirmed) return;
        }

        Notice = null;
        CloseReportText = null;
        IsClosing = true;
        CloseReport? report;
        try
        {
            report = await Task.Run(() => _services.CloseService.Close(id));
        }
        finally
        {
            IsClosing = false;
        }
        await ShowCloseReportAsync(report);
        Refresh();
    }

    /// <summary>"Title - App", or just one of them when they say the same ("Zeichentabelle - Zeichentabelle" reads like a stutter).</summary>
    private static string WindowLabel(OpenWindowInfo window)
    {
        var app = AppNames.ForProcess(window.ProcessId, window.ProcessName);
        var title = window.Title.Trim();
        return title.Length == 0 ? app
            : string.Equals(title, app, StringComparison.OrdinalIgnoreCase) || title.EndsWith(" - " + app, StringComparison.OrdinalIgnoreCase) ? title
            : $"{title} - {app}";
    }

    /// <summary>All closed is one calm line; programs that stayed get a row each, with a way to them.</summary>
    private async Task ShowCloseReportAsync(CloseReport? report)
    {
        if (report is null || report.Requested == 0) return;

        _closeReport = report;
        _closeReportActiveId = _switcher.ActiveCategoryId;
        var programs = report.StillOpen.Count == 0
            ? []
            : await Task.Run(() => _services.StubbornPrograms.Describe(report.StillOpen));
        if (programs.Count == 0)
        {
            CloseReportText = ClosedLine(report);
            return;
        }

        var closed = report.Requested - programs.Sum(p => p.Windows.Count);
        CloseReportText = $"{report.CategoryName}: {closed} of {report.Requested} windows closed. "
            + (programs.Count == 1 ? "1 program" : $"{programs.Count} programs") + " kept running.";
        Replace(StubbornRows, programs.Select(p => new StubbornRow(p)));
    }

    private static string ClosedLine(CloseReport report) =>
        $"{report.CategoryName} closed - {(report.Requested == 1 ? "1 window" : $"{report.Requested} windows")}.";

    /// <summary>"Show": goes to the category if it is parked, and brings the program's window to the front.</summary>
    [RelayCommand]
    private async Task ShowProgramAsync(StubbornRow row)
    {
        if (_closeReport is { } report && _switcher.ActiveCategoryId != report.CategoryId)
        {
            // The banner stays: the other rows are still open business.
            _closeReportActiveId = report.CategoryId;
            ShowFailures(await Task.Run(() => _switcher.SwitchTo(report.CategoryId)));
        }
        var open = row.Program.Windows.FirstOrDefault(w => _services.WindowManager.IsWindowOpen(w.Handle));
        if (open is not null)
        {
            // A tray app hid its own window when it was asked to close; "Show" has to bring it back, not just activate it.
            if (!_services.WindowManager.IsWindowVisible(open.Handle)) _services.WindowManager.Show(open.Handle);
            _services.WindowManager.BringToFront(open.Handle);
        }
        Refresh();
    }

    /// <summary>First step of ending a program: looks again at what it would take along, then asks.</summary>
    [RelayCommand]
    private async Task ArmEndAsync(StubbornRow row)
    {
        row.ResultText = null;
        var fresh = await Task.Run(() => _services.StubbornPrograms.Describe(row.Program.Windows));
        if (fresh.FirstOrDefault(p => p.ProcessId == row.ProcessId) is not { } program)
        {
            // It went away on its own while the banner was up.
            await RemoveRowAsync(row);
            return;
        }
        row.Program = program;
        row.IsConfirming = true;
    }

    [RelayCommand]
    private void CancelEnd(StubbornRow row)
    {
        row.IsConfirming = false;
        // Opened from a window's menu there is nothing else to show, so the banner goes too.
        if (_closeReport is null) CloseReportText = null;
    }

    /// <summary>Second step: ends the process. What cannot be done is said in the row, in words that name what to do.</summary>
    [RelayCommand]
    private async Task EndProgramAsync(StubbornRow row)
    {
        row.IsConfirming = false;
        row.IsEnding = true;
        ProgramEndResult result;
        try
        {
            result = await Task.Run(() => _services.StubbornPrograms.End(row.Program));
        }
        finally
        {
            row.IsEnding = false;
        }

        if (result.IsDone)
        {
            await RemoveRowAsync(row);
            Refresh();
            return;
        }

        row.ResultText = result.Outcome switch
        {
            ProgramEndOutcome.Protected => $"{row.Name} is never ended from here. Close it yourself.",
            ProgramEndOutcome.AccessDenied => $"{row.Name} runs as administrator, so Catogarizer can't end it. Close it yourself or use Task Manager.",
            _ => $"Couldn't end {row.Name}. Try again, or end it in Task Manager.",
        };
        Refresh();
    }

    /// <summary>The row fades out; when it was the last, the banner settles into the calm "closed" line.</summary>
    private async Task RemoveRowAsync(StubbornRow row)
    {
        row.IsDissolving = true;
        await Task.Delay(220);
        StubbornRows.Remove(row);
        if (StubbornRows.Count > 0) return;
        CloseReportText = _closeReport is { } report ? ClosedLine(report) : $"{row.Name} ended.";
    }

    /// <summary>Right-click a window, "End program...": the same row as after a close, already at the second step.</summary>
    public async Task EndProgramFromMenuAsync(WindowRow window)
    {
        var info = new OpenWindowInfo(window.Handle, window.Title, window.ProcessName, window.ProcessId);
        var programs = await Task.Run(() => _services.StubbornPrograms.Describe([info]));
        if (programs.Count == 0) return;

        Notice = null;
        _closeReport = null;
        _closeReportActiveId = _switcher.ActiveCategoryId;
        CloseReportText = "Ending a program closes all of its windows at once.";
        Replace(StubbornRows, [new StubbornRow(programs[0]) { IsConfirming = !programs[0].IsProtected }]);
        NoticeRaised?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void DismissCloseReport() => CloseReportText = null;

    [RelayCommand]
    private void EditActive()
    {
        if (CanEditActive) OpenEditor(_switcher.ActiveCategoryId);
    }

    [RelayCommand]
    private void EditCategory(ShelfItem item)
    {
        if (item.CanEdit) OpenEditor(item.Id);
    }

    private void OpenEditor(Guid categoryId, bool suggestOpenApps = false)
    {
        _dialogService.ShowCategoryEditor(new CategoryEditorViewModel(_services, _switcher, _themeService, categoryId) { SuggestOpenApps = suggestOpenApps });
        Refresh();
    }

    [RelayCommand]
    private async Task PinAppAsync()
    {
        var vm = new AppEditDialogViewModel(_services.InstalledAppFinder, headingOverride: "Keep an app always visible", relaxedValidation: true);
        var result = _dialogService.ShowAppEdit(vm);
        if (result is null) return;
        await Task.Run(() => _switcher.PinApp(result.Value.Name, result.Value.ExecutablePath));
        Refresh();
    }

    [RelayCommand]
    private async Task UnpinAsync(PinnedRow row)
    {
        await Task.Run(() => _switcher.Unpin(row.Id));
        Refresh();
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_library, _services.Autostart, _themeService, _switcher.ShowAllAndReset);
        vm.RequestOpenTriggers += (_, _) => OpenAutomation();
        var saved = _dialogService.ShowSettings(vm);
        if (saved && vm.HotkeyChanged)
            _onHotkeyChanged(_library.Settings.CommandPaletteHotkey);
        if (saved) _resources.RequestTick();
        Refresh();
    }

    [RelayCommand]
    private void OpenAutomation()
    {
        _dialogService.ShowTriggers(new TriggersViewModel(_library, _dialogService, _services.InstalledAppFinder, _services.TriggerRunner));
        Refresh();
    }

    // ---------------- Helpers ----------------

    private void ShowFailures(SwitchResult? result)
    {
        if (result is not { Failures.Count: > 0 }) return;
        _noticeCategoryId = result.CategoryId;
        NoticeCanEdit = result.CategoryId != CategorySwitchService.Uncategorized;
        Notice = DescribeFailures(result.Failures);
        NoticeRaised?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The view scrolls the banner into sight - a problem the user can't see isn't reported.</summary>
    public event EventHandler? NoticeRaised;

    [RelayCommand]
    private void OpenUpdate() => _updateNotifier.OpenDownloadPage();

    [RelayCommand]
    private void DismissUpdate()
    {
        _updateNotifier.Dismiss();
        Refresh();
    }

    [RelayCommand]
    private void EditNoticeCategory()
    {
        if (_noticeCategoryId is { } id && _library.Categories.Any(c => c.Id == id))
        {
            Notice = null;
            OpenEditor(id);
        }
    }

    /// <summary>A window row in the hero brings that window forward (restoring it if minimized).</summary>
    [RelayCommand]
    private void BringToFront(WindowRow row)
    {
        if (_services.WindowManager.IsWindowOpen(row.Handle))
            _services.WindowManager.BringToFront(row.Handle);
    }

    /// <summary>The window menu for one row, built when it opens so the Pin/Unpin wording is current.</summary>
    public IReadOnlyList<WindowMenuEntry> BuildWindowMenu(WindowRow row)
    {
        var entries = new List<WindowMenuEntry>();
        if (MoveTargets.Count > 0)
        {
            entries.Add(new MenuHeaderEntry("MOVE TO"));
            entries.AddRange(MoveTargets);
            entries.Add(new MenuSeparatorEntry());
        }
        entries.Add(new PinMenuEntry(_switcher.FindPinned(row.ProcessName) is null ? "Pin app (always visible)" : "Unpin app"));
        entries.Add(new FreezeMenuEntry(_library.FindFreezableApp(row.ProcessName) is null ? "Allow freezing while parked (stops its sound too)" : "Don't freeze this app"));
        entries.Add(new MenuSeparatorEntry());
        entries.Add(new EndProgramMenuEntry($"End {row.AppName}..."));
        return entries;
    }

    public static IReadOnlyList<WindowMenuEntry> BuildShelfMenu(ShelfItem item) =>
        item.CanClose ? [new CloseCategoryMenuEntry("Close windows...")] : [];

    /// <summary>Context menu on a window row: lets the app be suspended while its category is parked, or takes that back.</summary>
    public async Task ToggleFreezeAsync(WindowRow row)
    {
        Notice = null;
        var path = AppNames.ExecutableOf(row.ProcessId) ?? row.ProcessName;
        try
        {
            await Task.Run(() =>
            {
                if (_library.FindFreezableApp(row.ProcessName) is { } allowed) _library.RemoveFreezableApp(allowed.Id);
                else _library.AddFreezableApp(row.AppName, path);
            });
        }
        catch (ArgumentException)
        {
            _noticeCategoryId = _switcher.ActiveCategoryId;
            NoticeCanEdit = false;
            Notice = $"{row.AppName} couldn't be changed.";
            NoticeRaised?.Invoke(this, EventArgs.Empty);
        }
        _resources.RequestTick();
        Refresh();
    }

    /// <summary>"Free memory now" in the Resources card: pages out every parked app that may be touched, whatever the load.</summary>
    [RelayCommand]
    private async Task FreeMemoryNowAsync()
    {
        FreeNowText = null;
        _freeNowCategoryId = _switcher.ActiveCategoryId;
        var freed = await Task.Run(_resources.FreeMemoryNow);
        // Success is the "Freed ..." pill, which fires on its own; only an empty result needs words.
        if (freed is null) FreeNowText = "Nothing more to free right now.";
        Refresh();
    }

    /// <summary>Context menu on a window row: pins the window's app so it is never hidden, or unpins it.</summary>
    public async Task TogglePinAsync(WindowRow row)
    {
        Notice = null;
        var path = AppNames.ExecutableOf(row.ProcessId) ?? row.ProcessName;
        try
        {
            await Task.Run(() => _switcher.TogglePin(row.AppName, path, row.ProcessName));
        }
        catch (ArgumentException)
        {
            _noticeCategoryId = _switcher.ActiveCategoryId;
            NoticeCanEdit = false;
            Notice = $"{row.AppName} couldn't be pinned.";
            NoticeRaised?.Invoke(this, EventArgs.Empty);
        }
        Refresh();
    }

    /// <summary>
    /// Context menu on a window row: sends the window to another category (or Unsorted) at once,
    /// without switching. In a category other than the active one it is parked right away.
    /// </summary>
    public async Task MoveWindowAsync(IntPtr handle, Guid categoryId)
    {
        Notice = null;
        var moved = await Task.Run(() => _switcher.MoveWindow(handle, categoryId));
        if (moved)
        {
            Refresh();
            return;
        }
        // The window closed in the meantime, or the category was deleted.
        _noticeCategoryId = _switcher.ActiveCategoryId;
        NoticeCanEdit = false;
        Notice = "That window couldn't be moved - it was closed, or the category no longer exists.";
        NoticeRaised?.Invoke(this, EventArgs.Empty);
        Refresh();
    }

    [RelayCommand]
    private void ToggleAllWindows()
    {
        ShowAllWindows = !ShowAllWindows;
        Refresh();
    }

    public static string? DescribeFailures(IReadOnlyList<AppActionResult> failures) => failures.Count switch
    {
        0 => null,
        1 => failures[0].ErrorMessage,
        _ => $"{failures.Count} apps had a problem: {string.Join(" ", failures.Select(f => f.ErrorMessage))}",
    };

    private static Brush HeroGradient(Color color)
    {
        var brush = new LinearGradientBrush(
            Color.FromArgb(0x2A, color.R, color.G, color.B),
            Color.FromArgb(0x06, color.R, color.G, color.B),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        brush.Freeze();
        return brush;
    }

    private static string Duration(TimeSpan span) => span.TotalMinutes switch
    {
        < 60 => $"{(int)span.TotalMinutes} min",
        _ => span.Minutes == 0 ? $"{(int)span.TotalHours} h" : $"{(int)span.TotalHours} h {span.Minutes} min",
    };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        if (target.SequenceEqual(list)) return; // records compare by value: unchanged rows keep their visuals
        target.Clear();
        foreach (var item in list) target.Add(item);
    }
}
