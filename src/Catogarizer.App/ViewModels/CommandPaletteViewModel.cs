using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

/// <summary>
/// The fast path: search-as-you-type over categories, opened via a global
/// hotkey from anywhere, so switching category doesn't need the main window.
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly ICategoryActionService _categoryActionService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<Category> Results { get; } = new();

    public event EventHandler? RequestClose;

    public CommandPaletteViewModel(LibraryService library, ICategoryActionService categoryActionService)
    {
        _library = library;
        _categoryActionService = categoryActionService;
        UpdateResults();
    }

    partial void OnSearchTextChanged(string value) => UpdateResults();

    private void UpdateResults()
    {
        Results.Clear();
        var query = SearchText.Trim();
        var matches = query.Length == 0
            ? _library.Categories.AsEnumerable()
            : _library.Categories.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var category in matches.OrderBy(c => c.SortOrder))
            Results.Add(category);
    }

    [RelayCommand]
    private Task Open(Category category) => RunActionAsync(category, _categoryActionService.Open);

    [RelayCommand]
    private Task Minimize(Category category) => RunActionAsync(category, _categoryActionService.Minimize);

    [RelayCommand]
    private Task CloseCategory(Category category) => RunActionAsync(category, _categoryActionService.Close);

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    private async Task RunActionAsync(Category category, Func<IReadOnlyList<AppEntry>, CategoryActionResult> action)
    {
        var apps = category.AppIds
            .Select(id => _library.Apps.FirstOrDefault(a => a.Id == id))
            .Where(a => a is not null)
            .Cast<AppEntry>()
            .ToList();

        IsBusy = true;
        try
        {
            await Task.Run(() => action(apps));
        }
        finally
        {
            IsBusy = false;
        }
        Close();
    }
}
