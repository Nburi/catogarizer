using System.Collections.ObjectModel;
using Catogarizer.App.Services;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConfigStore _configStore;
    private readonly ICategoryActionService _actionService;
    private readonly IProcessLauncher _launcher;
    private readonly IDialogService _dialogService;
    private readonly IAutostartManager _autostartManager;
    private readonly IAppBlocker _appBlocker;
    private AppConfig _config = new();

    public event EventHandler<AppBlockedEventArgs>? AppBlocked;

    public ObservableCollection<CategoryViewModel> Categories { get; } = new();
    public ObservableCollection<BlockedApp> BlockedApps { get; } = new();

    [ObservableProperty]
    private string newBlockedAppName = string.Empty;

    [ObservableProperty]
    private CategoryViewModel? selectedCategory;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isSettingsOpen;

    public SettingsViewModel? Settings { get; private set; }

    public bool HasCategories => Categories.Count > 0;
    public bool ShowEmptyState => !IsLoading && !IsSettingsOpen && !HasCategories;
    public bool ShowCategoryContent => !IsLoading && !IsSettingsOpen && HasCategories && SelectedCategory is not null;
    public bool ShowSettingsContent => !IsLoading && IsSettingsOpen;

    public MainViewModel(
        IConfigStore configStore,
        ICategoryActionService actionService,
        IProcessLauncher launcher,
        IDialogService dialogService,
        IAutostartManager autostartManager,
        IAppBlocker appBlocker)
    {
        _configStore = configStore;
        _actionService = actionService;
        _launcher = launcher;
        _dialogService = dialogService;
        _autostartManager = autostartManager;
        _appBlocker = appBlocker;
        _appBlocker.AppBlocked += (sender, args) => AppBlocked?.Invoke(this, args);
        Categories.CollectionChanged += (_, _) => NotifyDerivedState();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            _config = await _configStore.LoadAsync();
            Categories.Clear();
            foreach (var category in _config.Categories.OrderBy(c => c.Order))
                Categories.Add(CreateCategoryViewModel(category));

            SelectedCategory = Categories.FirstOrDefault();
            Settings = new SettingsViewModel(_config.Settings, _autostartManager, PersistAsync);
            OnPropertyChanged(nameof(Settings));

            BlockedApps.Clear();
            foreach (var blocked in _config.BlockedApps)
                BlockedApps.Add(blocked);
            _appBlocker.Start(_config.BlockedApps);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddBlockedAppAsync()
    {
        var processName = NewBlockedAppName.Trim();
        if (string.IsNullOrEmpty(processName))
            return;

        var blocked = new BlockedApp { ProcessName = processName };
        _config.BlockedApps.Add(blocked);
        BlockedApps.Add(blocked);
        NewBlockedAppName = string.Empty;

        _appBlocker.UpdateBlockList(_config.BlockedApps);
        await PersistAsync();
    }

    [RelayCommand]
    private async Task RemoveBlockedAppAsync(BlockedApp? blocked)
    {
        if (blocked is null)
            return;

        _config.BlockedApps.Remove(blocked);
        BlockedApps.Remove(blocked);

        _appBlocker.UpdateBlockList(_config.BlockedApps);
        await PersistAsync();
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        var name = _dialogService.ShowCategoryEdit(null, _config.Categories);
        if (name is null)
            return;

        var category = new Category { Name = name, Order = _config.Categories.Count };
        _config.Categories.Add(category);

        var viewModel = CreateCategoryViewModel(category);
        Categories.Add(viewModel);
        SelectedCategory = viewModel;

        await PersistAsync();
    }

    [RelayCommand]
    private async Task EditCategoryAsync(CategoryViewModel? categoryViewModel)
    {
        if (categoryViewModel is null)
            return;

        var name = _dialogService.ShowCategoryEdit(categoryViewModel.Model, _config.Categories);
        if (name is null)
            return;

        categoryViewModel.Model.Name = name;
        categoryViewModel.NotifyNameChanged();
        await PersistAsync();
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryViewModel? categoryViewModel)
    {
        if (categoryViewModel is null)
            return;

        var confirmed = _dialogService.ShowConfirm(
            "Delete category",
            $"Delete \"{categoryViewModel.Name}\" and its {categoryViewModel.Apps.Count} app(s)? This can't be undone.");
        if (!confirmed)
            return;

        _config.Categories.Remove(categoryViewModel.Model);
        var wasSelected = ReferenceEquals(SelectedCategory, categoryViewModel);
        Categories.Remove(categoryViewModel);
        if (wasSelected)
            SelectedCategory = Categories.FirstOrDefault();

        await PersistAsync();
    }

    private CategoryViewModel CreateCategoryViewModel(Category category) =>
        new(category, _actionService, _launcher, _dialogService, PersistAsync);

    private Task PersistAsync() => _configStore.SaveAsync(_config);

    partial void OnIsLoadingChanged(bool value) => NotifyDerivedState();

    partial void OnIsSettingsOpenChanged(bool value) => NotifyDerivedState();

    partial void OnSelectedCategoryChanged(CategoryViewModel? value) => NotifyDerivedState();

    private void NotifyDerivedState()
    {
        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowCategoryContent));
        OnPropertyChanged(nameof(ShowSettingsContent));
    }
}
