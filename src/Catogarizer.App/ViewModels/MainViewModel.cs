using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Catogarizer.Core.Persistence;
using Catogarizer.Core.Services;

namespace Catogarizer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConfigStore _configStore;
    private readonly ICategoryActionService _actionService;
    private readonly IProcessLauncher _launcher;

    public ObservableCollection<CategoryViewModel> Categories { get; } = new();

    [ObservableProperty]
    private CategoryViewModel? selectedCategory;

    [ObservableProperty]
    private bool isLoading;

    public bool HasCategories => Categories.Count > 0;
    public bool ShowEmptyState => !IsLoading && !HasCategories;
    public bool ShowCategoryContent => !IsLoading && HasCategories && SelectedCategory is not null;

    public MainViewModel(IConfigStore configStore, ICategoryActionService actionService, IProcessLauncher launcher)
    {
        _configStore = configStore;
        _actionService = actionService;
        _launcher = launcher;
        Categories.CollectionChanged += (_, _) => NotifyDerivedState();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var config = await _configStore.LoadAsync();
            Categories.Clear();
            foreach (var category in config.Categories.OrderBy(c => c.Order))
                Categories.Add(new CategoryViewModel(category, _actionService, _launcher));

            SelectedCategory = Categories.FirstOrDefault();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnIsLoadingChanged(bool value) => NotifyDerivedState();

    partial void OnSelectedCategoryChanged(CategoryViewModel? value) => NotifyDerivedState();

    private void NotifyDerivedState()
    {
        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowCategoryContent));
    }
}
