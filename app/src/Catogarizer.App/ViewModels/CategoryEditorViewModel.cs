using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Catogarizer.App.Services;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using Catogarizer.Core.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed partial class HueOption : ObservableObject
{
    public required double Hue { get; init; }
    public required string Name { get; init; }
    public required Brush Brush { get; init; }
    [ObservableProperty] private bool _isSelected;

    public override string ToString() => IsSelected ? $"{Name}, selected" : Name;
}

/// <summary>An open app that could be added to the template ("what you're already using").</summary>
public sealed record OpenAppSuggestion(string Name, string ExecutablePath, string WindowTitle)
{
    public override string ToString() => $"{Name}, {WindowTitle}";
}

/// <summary>
/// Everything about one category: name, color, the apps it opens with, what it holds back.
/// Changes save immediately (like the rest of the library), except the name, which saves once valid.
/// </summary>
public partial class CategoryEditorViewModel : ObservableObject
{
    private readonly EditorServices _services;
    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly ThemeService _themeService;

    public Guid CategoryId { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private string _name;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private string? _nameError;
    [ObservableProperty] private Brush _color = Brushes.Gray;
    [ObservableProperty] private bool _showOpenApps;
    [ObservableProperty] private bool _isLoadingOpenApps;
    [ObservableProperty] private string? _notice;
    [ObservableProperty] private string? _lastAddedText;

    public ObservableCollection<HueOption> Hues { get; } = new();
    public ObservableCollection<AppEntry> Apps { get; } = new();
    public ObservableCollection<BlockedApp> Blocked { get; } = new();
    public ObservableCollection<OpenAppSuggestion> OpenApps { get; } = new();

    public event EventHandler? RequestClose;

    public CategoryEditorViewModel(EditorServices services, CategorySwitcher switcher, ThemeService themeService, Guid categoryId)
    {
        _services = services;
        _library = services.Library;
        _switcher = switcher;
        _themeService = themeService;
        CategoryId = categoryId;
        _name = Category.Name;
        RefreshColors();
        RefreshLists();
    }

    private Category Category => _library.Categories.First(c => c.Id == CategoryId);

    // ---------------- Name & color ----------------

    partial void OnNameChanged(string value)
    {
        var validation = CategoryValidator.ValidateName(value, _library.Categories, excludingId: CategoryId);
        NameError = validation.IsValid ? null : validation.ErrorMessage;
        // Saved as you type, like everything else in the editor - closing never has to guess.
        CommitName();
    }

    private void CommitName()
    {
        if (NameError is not null || string.Equals(Name.Trim(), Category.Name, StringComparison.Ordinal)) return;
        _library.RenameCategory(CategoryId, Name);
    }

    [RelayCommand]
    private void PickHue(HueOption option)
    {
        _library.SetCategoryHue(CategoryId, option.Hue);
        RefreshColors();
    }

    private void RefreshColors()
    {
        var current = Category.Hue ?? CategoryHues.Palette[0];
        Color = ThemeService.FrozenBrush(_themeService.CategoryColor(current));
        Hues.Clear();
        for (var i = 0; i < CategoryHues.Palette.Count; i++)
        {
            var hue = CategoryHues.Palette[i];
            Hues.Add(new HueOption
            {
                Hue = hue,
                Name = CategoryHues.Names[i],
                Brush = ThemeService.FrozenBrush(_themeService.CategoryColor(hue)),
                IsSelected = Math.Abs(hue - current) < 0.5,
            });
        }
    }

    // ---------------- Apps it opens with ----------------

    private void RefreshLists()
    {
        Apps.Clear();
        foreach (var app in _library.AppsOf(Category)) Apps.Add(app);
        Blocked.Clear();
        foreach (var blocked in _library.BlockedAppsOf(Category)) Blocked.Add(blocked);
    }

    [RelayCommand]
    private void AddApp()
    {
        var result = _services.Dialogs.ShowAppEdit(new AppEditDialogViewModel(_services.InstalledAppFinder));
        if (result is null) return;
        var app = _library.AddOrReuseApp(result.Value.Name, result.Value.ExecutablePath, result.Value.Arguments);
        _library.AddAppToCategory(CategoryId, app.Id);
        RefreshLists();
    }

    /// <summary>A brand-new, empty category starts with its open apps suggested.</summary>
    public bool SuggestOpenApps { get; init; }

    [RelayCommand]
    private async Task ToggleOpenAppsAsync()
    {
        ShowOpenApps = !ShowOpenApps;
        if (ShowOpenApps) await LoadOpenAppsAsync();
    }

    /// <summary>The list is a snapshot; refreshed whenever the editor gets focus back.</summary>
    public async Task RefreshOpenAppsAsync()
    {
        if (ShowOpenApps && !IsLoadingOpenApps) await LoadOpenAppsAsync();
    }

    private async Task LoadOpenAppsAsync()
    {
        IsLoadingOpenApps = true;
        OpenApps.Clear();
        // By exe name, not full path: Windows 11 Notepad runs from WindowsApps, not C:\Windows\notepad.exe.
        var known = Apps.Select(a => Path.GetFileNameWithoutExtension(a.ExecutablePath))
            .Concat(_library.PinnedApps.Select(p => ProcessPattern.ProcessName(p.ProcessNameOrPath)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suggestions = await Task.Run(() => FindOpenApps(known));
        foreach (var suggestion in suggestions) OpenApps.Add(suggestion);
        IsLoadingOpenApps = false;
    }

    /// <param name="knownExeNames">Exe names already in the template or pinned - not worth suggesting.</param>
    private List<OpenAppSuggestion> FindOpenApps(HashSet<string> knownExeNames)
    {
        var ownProcess = Environment.ProcessId;
        var result = new List<OpenAppSuggestion>();
        foreach (var window in _services.WindowFinder.FindAllAppWindows())
        {
            // UWP windows all belong to one host process, whose exe can't relaunch the app.
            if (window.ProcessId == ownProcess || window.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)) continue;
            if (AppNames.ExecutableOf(window.ProcessId) is not { } path) continue;
            if (knownExeNames.Contains(Path.GetFileNameWithoutExtension(path)) || result.Any(r => r.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(new OpenAppSuggestion(AppNames.ForExecutable(path, window.ProcessName), path, window.Title));
        }
        return result.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    [RelayCommand]
    private void AddOpenApp(OpenAppSuggestion suggestion)
    {
        var app = _library.AddOrReuseApp(suggestion.Name, suggestion.ExecutablePath);
        _library.AddAppToCategory(CategoryId, app.Id);
        OpenApps.Remove(suggestion);
        RefreshLists();
        // The new row lands below this panel, often out of sight - say it worked, right here.
        LastAddedText = $"Added {app.Name}. It's listed below and opens with this category.";
    }

    [RelayCommand]
    private void EditApp(AppEntry app)
    {
        var result = _services.Dialogs.ShowAppEdit(new AppEditDialogViewModel(app));
        if (result is null) return;
        _library.UpdateApp(app.Id, result.Value.Name, result.Value.ExecutablePath, result.Value.Arguments);
        RefreshLists();
    }

    [RelayCommand]
    private void RemoveApp(AppEntry app)
    {
        _library.RemoveAppFromCategory(CategoryId, app.Id);
        RefreshLists();
    }

    [RelayCommand]
    private void SetPlacement(AppEntry app)
    {
        var vm = new PlacementDialogViewModel(app, _services.ProcessLauncher, _services.WindowFinder, _services.WindowManager, _services.MonitorService);
        var (saved, placement) = _services.Dialogs.ShowPlacement(vm);
        if (!saved) return;
        _library.SetAppPlacement(app.Id, placement);
        RefreshLists();
    }

    [RelayCommand]
    private Task OpenAppAsync(AppEntry app) => RunAppActionAsync(() => _services.CategoryActions.OpenApp(app));

    [RelayCommand]
    private Task MinimizeAppAsync(AppEntry app) => RunAppActionAsync(() => _services.CategoryActions.MinimizeApp(app));

    [RelayCommand]
    private Task CloseAppAsync(AppEntry app) => RunAppActionAsync(() => _services.CategoryActions.CloseApp(app));

    private async Task RunAppActionAsync(Func<AppActionResult> action)
    {
        Notice = null;
        var result = await Task.Run(action);
        if (result.Outcome == AppActionOutcome.Failed) Notice = result.ErrorMessage;
    }

    // ---------------- Held back ----------------

    [RelayCommand]
    private void AddBlocked()
    {
        var vm = new AppEditDialogViewModel(_services.InstalledAppFinder, headingOverride: "Hold back an app", relaxedValidation: true);
        var result = _services.Dialogs.ShowAppEdit(vm);
        if (result is null) return;
        _switcher.HoldBack(CategoryId, result.Value.Name, result.Value.ExecutablePath);
        RefreshLists();
    }

    [RelayCommand]
    private void RemoveBlocked(BlockedApp blocked)
    {
        _switcher.StopHoldingBack(CategoryId, blocked.Id);
        RefreshLists();
    }

    // ---------------- Closing ----------------

    [RelayCommand]
    private void Delete()
    {
        var confirmed = _services.Dialogs.ShowConfirm(
            "Delete category?",
            $"\"{Category.Name}\" will be removed. Its open windows move to Unsorted - nothing is closed, and the apps themselves aren't touched.");
        if (!confirmed) return;
        _switcher.DeleteCategory(CategoryId);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Done() => RequestClose?.Invoke(this, EventArgs.Empty);

    private bool CanFinish() => NameError is null;

}
