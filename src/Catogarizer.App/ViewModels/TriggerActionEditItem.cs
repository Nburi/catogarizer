using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Catogarizer.App.Services;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed record ActionTypeOption(TriggerActionType Value, string Label);

/// <summary>One row in a trigger's action list editor. Wraps an in-progress TriggerAction -
/// converts to/from the real model only on Save, so cancelling the dialog discards edits.</summary>
public sealed partial class TriggerActionEditItem : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IInstalledAppFinder _installedAppFinder;
    private readonly IDialogService _dialogService;

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

    /// <summary>Shared across every action row in the same trigger editor (owned by
    /// TriggerEditDialogViewModel) - adding an app from one row's "+ Add app" makes it
    /// available in every other row immediately, since ObservableCollection change
    /// notifications drive both the ComboBoxes and each row's AppSelections checklist.</summary>
    public ObservableCollection<AppEntry> AvailableApps { get; }

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

    public TriggerActionEditItem(LibraryService library, IInstalledAppFinder installedAppFinder, IDialogService dialogService,
        IReadOnlyList<Category> categories, ObservableCollection<AppEntry> apps, TriggerAction? source = null)
    {
        _library = library;
        _installedAppFinder = installedAppFinder;
        _dialogService = dialogService;
        AvailableCategories = categories;
        AvailableApps = apps;
        _type = source?.Type ?? TriggerActionType.OpenCategory;
        _selectedCategory = categories.FirstOrDefault(c => c.Id == source?.CategoryId);
        _selectedApp = apps.FirstOrDefault(a => a.Id == source?.AppId);
        AppSelections = new ObservableCollection<AppCloseSelection>(
            apps.Select(a => new AppCloseSelection(a, source?.AppIdsToClose.Contains(a.Id) ?? false)));
        AvailableApps.CollectionChanged += OnAvailableAppsChanged;
    }

    private void OnAvailableAppsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        foreach (AppEntry added in e.NewItems)
            if (AppSelections.All(s => s.App.Id != added.Id))
                AppSelections.Add(new AppCloseSelection(added, isChecked: false));
    }

    /// <summary>Same "search installed apps or browse for the .exe" flow as adding an app to a
    /// category from the main window - the trigger editor otherwise has no way to reference an
    /// app that isn't already in the library.</summary>
    [RelayCommand]
    private void AddApp()
    {
        var vm = new AppEditDialogViewModel(_installedAppFinder);
        var result = _dialogService.ShowAppEdit(vm);
        if (result is null) return;

        var app = _library.AddOrReuseApp(result.Value.Name, result.Value.ExecutablePath, result.Value.Arguments);
        if (AvailableApps.All(a => a.Id != app.Id))
            AvailableApps.Add(app);

        if (Type == TriggerActionType.OpenApp)
        {
            SelectedApp = app;
        }
        else if (Type == TriggerActionType.CloseApps)
        {
            var selection = AppSelections.FirstOrDefault(s => s.App.Id == app.Id);
            if (selection is not null) selection.IsChecked = true;
        }
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
