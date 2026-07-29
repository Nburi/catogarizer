using System.Collections.ObjectModel;
using System.Linq;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConfigStore _configStore;

    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty]
    private bool _hasCategories;

    public MainViewModel(IConfigStore configStore)
    {
        _configStore = configStore;
        Load();
    }

    private void Load()
    {
        var config = _configStore.Load();

        Categories.Clear();
        foreach (var category in config.Categories.OrderBy(c => c.SortOrder))
            Categories.Add(category);

        HasCategories = Categories.Count > 0;
    }
}
