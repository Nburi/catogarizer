using System.Collections.ObjectModel;
using System.Linq;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public sealed record ActionTypeOption(TriggerActionType Value, string Label);

/// <summary>One row in a trigger's action list editor. Wraps an in-progress TriggerAction -
/// converts to/from the real model only on Save, so cancelling the dialog discards edits.</summary>
public sealed partial class TriggerActionEditItem : ObservableObject
{
    // Instance property (not static) - WPF's binding engine resolves paths against the
    // DataContext instance and doesn't reliably find static members without an x:Static
    // binding, so this is exposed per-row even though the list itself never varies.
    public IReadOnlyList<ActionTypeOption> ActionTypeOptions { get; } =
    [
        new(TriggerActionType.OpenCategory, "Open category"),
        new(TriggerActionType.OpenApp, "Open app"),
        new(TriggerActionType.CloseApps, "Close apps"),
    ];

    public IReadOnlyList<Category> AvailableCategories { get; }
    public IReadOnlyList<AppEntry> AvailableApps { get; }
    public ObservableCollection<AppCloseSelection> AppSelections { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenCategory))]
    [NotifyPropertyChangedFor(nameof(IsOpenApp))]
    [NotifyPropertyChangedFor(nameof(IsCloseApps))]
    private TriggerActionType _type;

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private AppEntry? _selectedApp;

    public bool IsOpenCategory => Type == TriggerActionType.OpenCategory;
    public bool IsOpenApp => Type == TriggerActionType.OpenApp;
    public bool IsCloseApps => Type == TriggerActionType.CloseApps;

    public TriggerActionEditItem(IReadOnlyList<Category> categories, IReadOnlyList<AppEntry> apps, TriggerAction? source = null)
    {
        AvailableCategories = categories;
        AvailableApps = apps;
        _type = source?.Type ?? TriggerActionType.OpenCategory;
        _selectedCategory = categories.FirstOrDefault(c => c.Id == source?.CategoryId);
        _selectedApp = apps.FirstOrDefault(a => a.Id == source?.AppId);
        AppSelections = new ObservableCollection<AppCloseSelection>(
            apps.Select(a => new AppCloseSelection(a, source?.AppIdsToClose.Contains(a.Id) ?? false)));
    }

    public TriggerAction ToTriggerAction() => new()
    {
        Type = Type,
        CategoryId = Type == TriggerActionType.OpenCategory ? SelectedCategory?.Id : null,
        AppId = Type == TriggerActionType.OpenApp ? SelectedApp?.Id : null,
        AppIdsToClose = Type == TriggerActionType.CloseApps
            ? AppSelections.Where(s => s.IsChecked).Select(s => s.App.Id).ToList()
            : new List<Guid>(),
    };
}
