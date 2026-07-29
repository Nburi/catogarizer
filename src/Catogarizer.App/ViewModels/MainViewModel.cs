using System.Collections.ObjectModel;
using System.Linq;
using Catogarizer.App.Services;
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

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<AppEntry> SelectedCategoryApps { get; } = new();

    public bool HasCategories => Categories.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCategory))]
    private Category? _selectedCategory;

    public bool HasSelectedCategory => SelectedCategory is not null;

    public MainViewModel(LibraryService library, IInstalledAppFinder installedAppFinder, IDialogService dialogService,
        IProcessLauncher processLauncher, IWindowFinder windowFinder, IWindowManager windowManager, IMonitorService monitorService)
    {
        _library = library;
        _installedAppFinder = installedAppFinder;
        _dialogService = dialogService;
        _processLauncher = processLauncher;
        _windowFinder = windowFinder;
        _windowManager = windowManager;
        _monitorService = monitorService;
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

    [RelayCommand]
    private void SelectCategory(Category category)
    {
        SelectedCategory = category;
        RefreshSelectedCategoryApps();
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
            $"\"{category.Name}\" will be removed. Its apps stay in your library and can be added to another category.");
        if (!confirmed) return;

        _library.DeleteCategory(category.Id);
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
    private void SetPlacement(AppEntry app)
    {
        var vm = new PlacementDialogViewModel(app, _processLauncher, _windowFinder, _windowManager, _monitorService);
        var (saved, placement) = _dialogService.ShowPlacement(vm);
        if (!saved) return;

        _library.SetAppPlacement(app.Id, placement);
        RefreshCategories();
    }
}
