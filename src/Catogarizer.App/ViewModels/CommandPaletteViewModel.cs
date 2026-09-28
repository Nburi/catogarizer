using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Catogarizer.App.Services;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed partial class PaletteEntry : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string? KeyText { get; init; }
    public required Brush Color { get; init; }
    public required string StateText { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsPrevious { get; init; }
    public required bool HasMissingApp { get; init; }
    public required IReadOnlyList<TemplateIcon> Icons { get; init; }

    public string PreviousLabel => IsPrevious ? "   last used" : "";

    [ObservableProperty] private bool _isSelected;

    public override string ToString() => $"{Name}, {StateText}";
}

/// <summary>
/// The everyday surface (PRINCIPLES.md, values 2 and 3): opened by the hotkey from anywhere.
/// Digits jump straight to a category, typing filters, Enter switches the selection, which
/// starts on the previous category - the most likely place to go.
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject
{
    private const int MaxIcons = 3;

    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly ThemeService _themeService;
    private readonly Action<SwitchResult> _onSwitched;
    private List<PaletteEntry> _all = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<PaletteEntry> Results { get; } = new();

    /// <summary>"Always visible: Spotify, WhatsApp", or null when nothing is pinned.</summary>
    public string? PinnedText { get; }

    public event EventHandler? RequestClose;

    public CommandPaletteViewModel(LibraryService library, CategorySwitcher switcher, ThemeService themeService, Action<SwitchResult> onSwitched)
    {
        _library = library;
        _switcher = switcher;
        _themeService = themeService;
        _onSwitched = onSwitched;
        BuildEntries();
        PinnedText = library.PinnedApps.Count == 0 ? null : "Always visible: " + string.Join(", ", library.PinnedApps.Select(p => p.Name));
        UpdateResults();
    }

    private void BuildEntries()
    {
        var sessions = _switcher.GetSessions();
        var active = _switcher.ActiveCategoryId;
        var previous = _switcher.PreviousCategoryId;
        var categories = _library.Categories.OrderBy(c => c.SortOrder).ToList();

        PaletteEntry Entry(Guid id, string name, string? key, Color color, IReadOnlyList<AppEntry> template)
        {
            var parked = sessions.TryGetValue(id, out var w) ? w.Count : 0;
            // Same wording as the home's tiles.
            var missing = id == active || parked > 0 ? 0 : template.Count(a => !System.IO.File.Exists(a.ExecutablePath));
            var state = id == active ? "You're here"
                : parked > 0 ? $"{parked} parked"
                : id == CategorySwitchService.Uncategorized ? "Nothing parked"
                : missing > 0 ? (missing == 1 ? "1 app not found" : $"{missing} apps not found")
                : template.Count switch { 0 => "Empty", 1 => "Opens 1 app", var n => $"Opens {n} apps" };
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return new PaletteEntry
            {
                Id = id, Name = name, KeyText = key, Color = brush, StateText = state,
                IsActive = id == active, IsPrevious = id == previous, HasMissingApp = missing > 0,
                Icons = template.Take(MaxIcons).Select(a => new TemplateIcon(a.Name, a.ExecutablePath)).ToList(),
            };
        }

        _all = categories
            .Select((c, i) => Entry(c.Id, c.Name, i < 9 ? (i + 1).ToString() : null,
                c.Hue is { } hue ? _themeService.CategoryColor(hue) : Muted(), _library.AppsOf(c)))
            .Append(Entry(CategorySwitchService.Uncategorized, CategorySwitchService.UncategorizedName, "0", Muted(), []))
            .ToList();
    }

    private Color Muted() => ThemeService.ToColor(_themeService.Current.Muted.ToRgb());

    partial void OnSearchTextChanged(string value) => UpdateResults();

    private void UpdateResults()
    {
        var query = SearchText.Trim();
        var matches = query.Length == 0
            ? _all
            : _all.Select(e => (Entry: e, Score: CategoryMatcher.Score(e.Name, query)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Entry)
                .ToList();

        Results.Clear();
        foreach (var entry in matches)
        {
            entry.IsSelected = false;
            Results.Add(entry);
        }

        // With nothing typed, start on the previous category (the usual destination); otherwise the best match.
        var initial = query.Length == 0
            ? Results.FirstOrDefault(e => e.IsPrevious) ?? Results.FirstOrDefault(e => !e.IsActive)
            : Results.FirstOrDefault();
        if (initial is not null) initial.IsSelected = true;
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        var index = Results.ToList().FindIndex(e => e.IsSelected);
        var next = index < 0 ? 0 : (index + delta + Results.Count) % Results.Count;
        foreach (var e in Results) e.IsSelected = false;
        Results[next].IsSelected = true;
    }

    public Task SwitchSelectedAsync() =>
        Results.FirstOrDefault(e => e.IsSelected) is { } selected ? SwitchTo(selected) : Task.CompletedTask;

    /// <summary>Digits in an empty search box jump straight to that category (0 = Unsorted).</summary>
    public Task SwitchByNumberAsync(int number) =>
        _all.FirstOrDefault(e => e.KeyText == number.ToString()) is { } entry ? SwitchTo(entry) : Task.CompletedTask;

    [RelayCommand]
    private async Task SwitchTo(PaletteEntry entry)
    {
        if (IsBusy) return;
        if (entry.IsActive)
        {
            Close();
            return;
        }

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
