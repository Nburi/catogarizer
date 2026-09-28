using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed record PaletteEntry(Guid Id, string Name, string Detail, bool IsActive);

/// <summary>
/// The fast path: search-as-you-type over categories (and Unsorted), opened via a global
/// hotkey from anywhere. Enter or a click switches.
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly Action<SwitchResult> _onSwitched;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<PaletteEntry> Results { get; } = new();

    public event EventHandler? RequestClose;

    public CommandPaletteViewModel(LibraryService library, CategorySwitcher switcher, Action<SwitchResult> onSwitched)
    {
        _library = library;
        _switcher = switcher;
        _onSwitched = onSwitched;
        UpdateResults();
    }

    partial void OnSearchTextChanged(string value) => UpdateResults();

    private void UpdateResults()
    {
        var active = _switcher.ActiveCategoryId;
        var entries = _library.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new PaletteEntry(c.Id, c.Name, c.Id == active ? "Active" : $"{c.AppIds.Count} apps", c.Id == active))
            .Append(new PaletteEntry(CategorySwitchService.Uncategorized, CategorySwitchService.UncategorizedName,
                active == CategorySwitchService.Uncategorized ? "Active" : "Windows outside any category",
                active == CategorySwitchService.Uncategorized));

        var query = SearchText.Trim();
        Results.Clear();
        foreach (var entry in entries.Where(e => query.Length == 0 || e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            Results.Add(entry);
    }

    [RelayCommand]
    private async Task SwitchTo(PaletteEntry entry)
    {
        if (IsBusy) return;
        IsBusy = true;
        SwitchResult? result;
        try
        {
            result = await Task.Run(() => _switcher.SwitchTo(entry.Id));
        }
        finally
        {
            IsBusy = false;
        }
        if (result is not null) _onSwitched(result);
        Close();
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);
}
