using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Catogarizer.App.Services;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IInstalledAppFinder _installedAppFinder;
    private readonly IDialogService _dialogService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IWindowFinder _windowFinder;
    private readonly IWindowManager _windowManager;
    private readonly IMonitorService _monitorService;
    private readonly ICategoryActionService _categoryActionService;
    private readonly CategorySwitcher _switcher;
    private readonly ThemeService _themeService;
    private readonly IAutostartService _autostartService;
    private readonly TriggerRunner _triggerRunner;
    private readonly Action<string> _onHotkeyChanged;

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<AppEntry> SelectedCategoryApps { get; } = new();
    public ObservableCollection<BlockedApp> SelectedCategoryBlockedApps { get; } = new();

    public bool HasCategories => Categories.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCategory))]
    private Category? _selectedCategory;

    public bool HasSelectedCategory => SelectedCategory is not null;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private bool _noticeIsError;

    public MainViewModel(LibraryService library, IInstalledAppFinder installedAppFinder, IDialogService dialogService,
        IProcessLauncher processLauncher, IWindowFinder windowFinder, IWindowManager windowManager,
        IMonitorService monitorService, ICategoryActionService categoryActionService, CategorySwitcher switcher,
        IAutostartService autostartService, TriggerRunner triggerRunner, ThemeService themeService, Action<string> onHotkeyChanged)
    {
        _themeService = themeService;
        _library = library;
        _installedAppFinder = installedAppFinder;
        _dialogService = dialogService;
        _processLauncher = processLauncher;
        _windowFinder = windowFinder;
        _windowManager = windowManager;
        _monitorService = monitorService;
        _categoryActionService = categoryActionService;
        _switcher = switcher;
        _autostartService = autostartService;
        _triggerRunner = triggerRunner;
        _onHotkeyChanged = onHotkeyChanged;
        RefreshCategories();
    }

    private void RefreshCategories()
    {
        var previouslySelectedId = SelectedCategory?.Id;

        Categories.Clear();
        foreach (var category in _library.Categories.OrderBy(c => c.SortOrder))
            Categories.Add(category);
        OnPropertyChanged(nameof(HasCategories));

        SelectedCategory = previouslySelectedId is null ? null : Categories.FirstOrDefault(c => c.Id == previouslySelectedId);
        // Category doesn't implement INotifyPropertyChanged (it's a plain Core model), so a
        // rename that mutates the same instance in place won't raise change notifications on
        // its own - force one so anything bound to SelectedCategory.* (the detail panel header)
        // picks up the new value even when SelectedCategory is reference-equal to before.
        OnPropertyChanged(nameof(SelectedCategory));
        RefreshSelectedCategoryApps();
        RefreshSelectedCategoryBlockedApps();
    }

    private void RefreshSelectedCategoryApps()
    {
        SelectedCategoryApps.Clear();
        if (SelectedCategory is null) return;
        foreach (var appId in SelectedCategory.AppIds)
        {
            var app = _library.Apps.FirstOrDefault(a => a.Id == appId);
            if (app is not null) SelectedCategoryApps.Add(app);
        }
    }

    private void RefreshSelectedCategoryBlockedApps()
    {
        SelectedCategoryBlockedApps.Clear();
        if (SelectedCategory is null) return;
        foreach (var blocked in _library.BlockedAppsOf(SelectedCategory))
            SelectedCategoryBlockedApps.Add(blocked);
    }

    [RelayCommand]
    private void SelectCategory(Category category)
    {
        SelectedCategory = category;
        RefreshSelectedCategoryApps();
        RefreshSelectedCategoryBlockedApps();
    }

    [RelayCommand]
    private void AddCategory()
    {
        var vm = new CategoryEditDialogViewModel(_library.Categories);
        var name = _dialogService.ShowCategoryEdit(vm);
        if (name is null) return;

        var category = _library.AddCategory(name);
        RefreshCategories();
        SelectCategory(Categories.First(c => c.Id == category.Id));
    }

    [RelayCommand]
    private void RenameCategory(Category category)
    {
        var vm = new CategoryEditDialogViewModel(_library.Categories, category);
        var name = _dialogService.ShowCategoryEdit(vm);
        if (name is null) return;

        _library.RenameCategory(category.Id, name);
        RefreshCategories();
    }

    [RelayCommand]
    private void DeleteCategory(Category category)
    {
        var confirmed = _dialogService.ShowConfirm(
            "Delete category?",
            $"\"{category.Name}\" will be removed. Its open windows move to Unsorted, and its apps stay in your library for other categories.");
        if (!confirmed) return;

        _switcher.DeleteCategory(category.Id);
        RefreshCategories();
    }

    [RelayCommand]
    private void AddAppToCategory()
    {
        if (SelectedCategory is null) return;

        var vm = new AppEditDialogViewModel(_installedAppFinder);
        var result = _dialogService.ShowAppEdit(vm);
        if (result is null) return;

        var app = _library.AddOrReuseApp(result.Value.Name, result.Value.ExecutablePath, result.Value.Arguments);
        _library.AddAppToCategory(SelectedCategory.Id, app.Id);
        RefreshSelectedCategoryApps();
    }

    [RelayCommand]
    private void EditApp(AppEntry app)
    {
        var vm = new AppEditDialogViewModel(app);
        var result = _dialogService.ShowAppEdit(vm);
        if (result is null) return;

        _library.UpdateApp(app.Id, result.Value.Name, result.Value.ExecutablePath, result.Value.Arguments);
        RefreshCategories();
    }

    [RelayCommand]
    private void RemoveAppFromCategory(AppEntry app)
    {
        if (SelectedCategory is null) return;
        _library.RemoveAppFromCategory(SelectedCategory.Id, app.Id);
        RefreshSelectedCategoryApps();
    }

    [RelayCommand]
    private void AddBlockedAppToCategory()
    {
        if (SelectedCategory is null) return;

        var vm = new AppEditDialogViewModel(_installedAppFinder, headingOverride: "Block app", relaxedValidation: true);
        var result = _dialogService.ShowAppEdit(vm);
        if (result is null) return;

        var blocked = _library.AddOrReuseBlockedApp(result.Value.Name, result.Value.ExecutablePath);
        _library.AddBlockedAppToCategory(SelectedCategory.Id, blocked.Id);
        _switcher.RefreshBlocking();
        RefreshSelectedCategoryBlockedApps();
    }

    [RelayCommand]
    private void RemoveBlockedAppFromCategory(BlockedApp blocked)
    {
        if (SelectedCategory is null) return;
        _library.RemoveBlockedAppFromCategory(SelectedCategory.Id, blocked.Id);
        _switcher.RefreshBlocking();
        RefreshSelectedCategoryBlockedApps();
    }

    [RelayCommand]
    private void SetPlacement(AppEntry app)
    {
        var vm = new PlacementDialogViewModel(app, _processLauncher, _windowFinder, _windowManager, _monitorService);
        var (saved, placement) = _dialogService.ShowPlacement(vm);
        if (!saved) return;

        _library.SetAppPlacement(app.Id, placement);
        RefreshCategories();
    }

    /// <summary>No busy overlay: a switch that restores a session must feel instant (PRINCIPLES.md, value 2).</summary>
    [RelayCommand]
    private async Task SwitchToCategoryAsync(Category category)
    {
        Notice = null;
        var result = await Task.Run(() => _switcher.SwitchTo(category.Id));
        if (result is { Failures.Count: > 0 })
        {
            Notice = DescribeFailures(result.Failures);
            NoticeIsError = true;
        }
    }

    [RelayCommand]
    private Task OpenAppAsync(AppEntry app) =>
        RunBusyAsync($"Opening \"{app.Name}\"...", () => new CategoryActionResult([_categoryActionService.OpenApp(app)]));

    [RelayCommand]
    private Task MinimizeAppAsync(AppEntry app) =>
        RunBusyAsync($"Minimizing \"{app.Name}\"...", () => new CategoryActionResult([_categoryActionService.MinimizeApp(app)]));

    [RelayCommand]
    private Task CloseAppAsync(AppEntry app) =>
        RunBusyAsync($"Closing \"{app.Name}\"...", () => new CategoryActionResult([_categoryActionService.CloseApp(app)]));

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_library, _autostartService, _themeService);
        vm.RequestOpenTriggers += (_, _) => OpenTriggers();
        var saved = _dialogService.ShowSettings(vm);
        if (saved && vm.HotkeyChanged)
            _onHotkeyChanged(_library.Settings.CommandPaletteHotkey);
    }

    [RelayCommand]
    private void OpenTriggers()
    {
        var vm = new TriggersViewModel(_library, _dialogService, _installedAppFinder, _triggerRunner);
        _dialogService.ShowTriggers(vm);
    }

    private async Task RunBusyAsync(string busyMessage, Func<CategoryActionResult> action)
    {
        IsBusy = true;
        BusyMessage = busyMessage;
        Notice = null;
        try
        {
            var result = await Task.Run(action);
            if (result.Failures.Count > 0)
            {
                Notice = DescribeFailures(result.Failures);
                NoticeIsError = true;
            }
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    public static string? DescribeFailures(IReadOnlyList<AppActionResult> failures) => failures.Count switch
    {
        0 => null,
        1 => failures[0].ErrorMessage,
        _ => $"{failures.Count} apps had a problem: {string.Join(" ", failures.Select(f => f.ErrorMessage))}",
    };
}
