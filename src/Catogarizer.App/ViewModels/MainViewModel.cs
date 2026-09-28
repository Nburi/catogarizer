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
    private readonly ThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly Action<string> _onHotkeyChanged;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refreshDebounce;
    private (string Name, DateTime At, Guid CategoryId)? _lastBlocked;
    private Guid? _noticeCategoryId;

    // ---- Now ----
    [ObservableProperty] private string _activeName = "";
    [ObservableProperty] private bool _activeIsUnsorted;
    [ObservableProperty] private Brush _activeColor = Brushes.Gray;
    [ObservableProperty] private Brush _heroTint = Brushes.Transparent;
    [ObservableProperty] private string _activeSinceText = "";
    [ObservableProperty] private string _windowCountText = "";
    [ObservableProperty] private string? _moreWindowsText;
    [ObservableProperty] private string? _noWindowsText;
    [ObservableProperty] private string? _heldBackEmptyText;
    [ObservableProperty] private string? _lastBlockedText;
    [ObservableProperty] private string? _activeKeyText;

    public ObservableCollection<WindowRow> ActiveWindows { get; } = new();
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

    // ---- Footer / chrome ----
    [ObservableProperty] private string? _nextTriggerText;
    [ObservableProperty] private IReadOnlyList<string> _hotkeyKeys = [];
    [ObservableProperty] private string? _notice;
    [ObservableProperty] private bool _noticeIsError;

    public MainViewModel(EditorServices services, CategorySwitcher switcher, ThemeService themeService,
        IAppBlockingService appBlockingService, Action<string> onHotkeyChanged)
    {
        _services = services;
        _library = services.Library;
        _dialogService = services.Dialogs;
        _switcher = switcher;
        _themeService = themeService;
        _onHotkeyChanged = onHotkeyChanged;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _refreshDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) =>
        {
            _refreshDebounce!.Stop();
            Refresh();
        }, _dispatcher);
        _refreshDebounce.Stop();

        switcher.StateChanged += RequestRefresh;
        themeService.ThemeChanged += RequestRefresh;
        appBlockingService.AppBlocked += name =>
        {
            _lastBlocked = (name, DateTime.Now, _switcher.ActiveCategoryId);
            RequestRefresh();
        };

        Refresh();
    }

    /// <summary>Safe from any thread; bursts of events collapse into one refresh.</summary>
    public void RequestRefresh() => _dispatcher.BeginInvoke(() =>
    {
        _refreshDebounce.Stop();
        _refreshDebounce.Start();
    });

    public void Refresh()
    {
        var sessions = _switcher.GetSessions();
        var activeId = _switcher.ActiveCategoryId;
        var categories = _library.Categories.OrderBy(c => c.SortOrder).ToList();
        var active = categories.FirstOrDefault(c => c.Id == activeId);

        HasCategories = categories.Count > 0;
        HotkeyKeys = _library.Settings.CommandPaletteHotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        RefreshNow(active, sessions);
        RefreshBack(categories, sessions);
        RefreshShelf(categories, activeId, sessions);
        RefreshPinned();
        RefreshNextTrigger(categories);
    }

    private void RefreshNow(Category? active, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var activeId = _switcher.ActiveCategoryId;
        ActiveIsUnsorted = active is null;
        ActiveName = active?.Name ?? CategorySwitchService.UncategorizedName;
        ActiveKeyText = KeyFor(activeId);
        var color = ColorOf(active);
        ActiveColor = Frozen(new SolidColorBrush(color));
        // Unsorted has no color of its own; a gray tint would only muddy the theme.
        HeroTint = active is null ? Brushes.Transparent : HeroGradient(color);

        var since = _switcher.ActiveSince;
        var elapsed = DateTime.Now - since;
        ActiveSinceText = elapsed.TotalMinutes < 1 ? $"since {since:HH:mm}" : $"since {since:HH:mm} · {Duration(elapsed)}";

        var windows = sessions.TryGetValue(activeId, out var list) ? list : [];
        var opening = _switcher.IsOpeningApps && active is not null;
        WindowCountText = opening ? "opening apps..." : windows.Count switch { 0 => "No windows", 1 => "1 window", var n => $"{n} windows" };
        var shown = ShowAllWindows ? windows.Count : MaxWindowRows;
        Replace(ActiveWindows, windows.Take(shown).Select(w => new WindowRow(w.Title, AppNames.ForProcess(w.ProcessId, w.ProcessName), w.ProcessId)));
        MoreWindowsText = windows.Count <= MaxWindowRows ? null
            : ShowAllWindows ? "Show fewer"
            : $"Show {windows.Count - MaxWindowRows} more";
        NoWindowsText = windows.Count > 0 ? null
            : opening ? $"Opening {string.Join(", ", _library.AppsOf(active!).Select(a => a.Name))}..."
            : active is null ? "Nothing open outside your categories."
            : "Windows you open now join this category.";

        // A launch problem belongs to the switch that caused it; the next switch clears it.
        if (Notice is not null && _noticeCategoryId != activeId) Notice = null;

        var blocked = active is null ? [] : _library.BlockedAppsOf(active);
        Replace(HeldBack, blocked.Select(b => new HeldBackRow(b.Name, PathOrNull(b.ProcessNameOrPath), RunningProcessOf(b.ProcessNameOrPath))));
        HeldBackEmptyText = blocked.Count > 0 ? null
            : active is null ? "Unsorted never holds anything back."
            : "Nothing is held back here. Add distracting apps in the editor.";
        LastBlockedText = _lastBlocked is { } last && last.CategoryId == activeId
            ? $"{last.Name} tried to open at {last.At:HH:mm} and was closed."
            : null;
    }

    private void RefreshBack(List<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var previousId = _switcher.PreviousCategoryId;
        var previous = categories.FirstOrDefault(c => c.Id == previousId);
        HasPrevious = previousId is not null && (previous is not null || previousId == CategorySwitchService.Uncategorized);
        if (!HasPrevious) return;

        BackTitle = $"Back to {previous?.Name ?? CategorySwitchService.UncategorizedName}";
        BackHint = $"or press {_library.Settings.CommandPaletteHotkey} twice";
        var parked = sessions.TryGetValue(previousId!.Value, out var w) ? w.Count : 0;
        BackDetail = parked switch { 0 => "Opens fresh", 1 => "1 window parked", var n => $"{n} windows parked" };
        BackColor = Frozen(new SolidColorBrush(ColorOf(previous)));
    }

    private void RefreshShelf(List<Category> categories, Guid activeId, IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> sessions)
    {
        var items = new List<ShelfItem>();
        foreach (var category in categories.Where(c => c.Id != activeId))
        {
            var apps = _library.AppsOf(category);
            var parked = sessions.TryGetValue(category.Id, out var w) ? w.Count : 0;
            var missing = parked > 0 ? 0 : apps.Count(a => !File.Exists(a.ExecutablePath));
            items.Add(new ShelfItem
            {
                Id = category.Id,
                Name = category.Name,
                KeyText = KeyFor(category.Id),
                Color = Frozen(new SolidColorBrush(ColorOf(category))),
                IsUnsorted = false,
                Icons = apps.Take(MaxShelfIcons).Select(a => new TemplateIcon(a.Name, a.ExecutablePath)).ToList(),
                MoreIconsText = apps.Count > MaxShelfIcons ? $"+{apps.Count - MaxShelfIcons}" : null,
                StateText = parked > 0 ? $"{parked} parked"
                    : missing > 0 ? (missing == 1 ? "1 app not found" : $"{missing} apps not found")
                    : apps.Count switch { 0 => "Empty", 1 => "Opens 1 app", var n => $"Opens {n} apps" },
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
                KeyText = "0",
                Color = Frozen(new SolidColorBrush(ColorOf(null))),
                IsUnsorted = true,
                Icons = parkedApps.Take(MaxShelfIcons).Select(x => new TemplateIcon(AppNames.ForProcess(x.Window.ProcessId, x.Window.ProcessName), x.Path!)).ToList(),
                MoreIconsText = parkedApps.Count > MaxShelfIcons ? $"+{parkedApps.Count - MaxShelfIcons}" : null,
                StateText = parked > 0 ? $"{parked} parked" : "Nothing parked",
                HasParkedWindows = parked > 0,
                BlockedCount = 0,
                TemplateCount = 0,
                HasMissingApp = false,
            });
        }

        // Keep the item a switch is running on, so its "Opening..." state survives refreshes.
        var switching = Shelf.FirstOrDefault(s => s.IsSwitching)?.Id;
        Replace(Shelf, items);
        if (switching is { } id && Shelf.FirstOrDefault(s => s.Id == id) is { } still) still.IsSwitching = true;
    }

    private void RefreshPinned() =>
        Replace(Pinned, _library.PinnedApps.Select(p => new PinnedRow(p.Id, p.Name, PathOrNull(p.ProcessNameOrPath), RunningProcessOf(p.ProcessNameOrPath))));

    private static string? PathOrNull(string value) => LooksLikePath(value) ? value : null;

    /// <summary>For bare process names ("claude"): a running process to take the icon from, or 0.</summary>
    private static int RunningProcessOf(string value)
    {
        if (LooksLikePath(value)) return 0;
        var processes = System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(value.Trim()));
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
        var id = number == 0
            ? CategorySwitchService.Uncategorized
            : _library.Categories.OrderBy(c => c.SortOrder).Skip(number - 1).FirstOrDefault()?.Id;
        if (id is null) return;
        Notice = null;
        ShowFailures(await Task.Run(() => _switcher.SwitchTo(id.Value)));
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

    [RelayCommand]
    private void EditActive()
    {
        if (!ActiveIsUnsorted) OpenEditor(_switcher.ActiveCategoryId);
    }

    [RelayCommand]
    private void EditCategory(ShelfItem item)
    {
        if (!item.IsUnsorted) OpenEditor(item.Id);
    }

    private void OpenEditor(Guid categoryId, bool suggestOpenApps = false)
    {
        _dialogService.ShowCategoryEditor(new CategoryEditorViewModel(_services, _switcher, _themeService, categoryId) { SuggestOpenApps = suggestOpenApps });
        Refresh();
    }

    [RelayCommand]
    private void PinApp()
    {
        var vm = new AppEditDialogViewModel(_services.InstalledAppFinder, headingOverride: "Keep an app always visible", relaxedValidation: true);
        var result = _dialogService.ShowAppEdit(vm);
        if (result is null) return;
        _library.AddPinnedApp(result.Value.Name, result.Value.ExecutablePath);
        Task.Run(_switcher.ApplyPinnedApps);
        Refresh();
    }

    [RelayCommand]
    private void Unpin(PinnedRow row)
    {
        _library.RemovePinnedApp(row.Id);
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
        NoticeIsError = true;
        NoticeRaised?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The view scrolls the banner into sight - a problem the user can't see isn't reported.</summary>
    public event EventHandler? NoticeRaised;

    [RelayCommand]
    private void EditNoticeCategory()
    {
        if (_noticeCategoryId is { } id && _library.Categories.Any(c => c.Id == id))
        {
            Notice = null;
            OpenEditor(id);
        }
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

    private string? KeyFor(Guid categoryId)
    {
        if (categoryId == CategorySwitchService.Uncategorized) return "0";
        var index = _library.Categories.OrderBy(c => c.SortOrder).ToList().FindIndex(c => c.Id == categoryId);
        return index is >= 0 and < 9 ? (index + 1).ToString() : null;
    }

    private Color ColorOf(Category? category) =>
        category?.Hue is { } hue ? _themeService.CategoryColor(hue) : ThemeService.ToColor(_themeService.Current.Muted.ToRgb());

    private static Brush HeroGradient(Color color)
    {
        var brush = new LinearGradientBrush(
            Color.FromArgb(0x2A, color.R, color.G, color.B),
            Color.FromArgb(0x06, color.R, color.G, color.B),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        brush.Freeze();
        return brush;
    }

    private static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static string Duration(TimeSpan span) => span.TotalMinutes switch
    {
        < 60 => $"{(int)span.TotalMinutes} min",
        _ => span.Minutes == 0 ? $"{(int)span.TotalHours} h" : $"{(int)span.TotalHours} h {span.Minutes} min",
    };

    private static bool LooksLikePath(string value) => value.Contains('\\') || value.Contains('/');

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        if (target.SequenceEqual(list)) return; // records compare by value: unchanged rows keep their visuals
        target.Clear();
        foreach (var item in list) target.Add(item);
    }
}
