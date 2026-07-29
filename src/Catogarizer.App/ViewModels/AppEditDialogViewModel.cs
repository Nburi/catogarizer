using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Catogarizer.App.ViewModels;

/// <summary>
/// Covers both flows: picking a new app to add (search over installed apps,
/// or "can't find it" manual entry) and editing an existing app entry
/// (always manual mode, prefilled - you're correcting a specific entry, not
/// picking a new one).
/// </summary>
public partial class AppEditDialogViewModel : ObservableObject
{
    private readonly IInstalledAppFinder? _installedAppFinder;
    private readonly IReadOnlyList<InstalledApp> _allInstalledApps;

    [ObservableProperty]
    private bool _isManualMode;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<InstalledApp> FilteredApps { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _manualName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _manualPath = string.Empty;

    [ObservableProperty]
    private string _manualArguments = string.Empty;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _pathError;

    public string HeadingText => IsEditing ? "Edit app" : "Add app";

    public (string Name, string ExecutablePath, string? Arguments)? Result { get; private set; }

    public event EventHandler? RequestClose;

    /// <summary>Add-flow constructor: starts in search mode.</summary>
    public AppEditDialogViewModel(IInstalledAppFinder installedAppFinder)
    {
        _installedAppFinder = installedAppFinder;
        _allInstalledApps = installedAppFinder.FindInstalledApps();
        UpdateFilteredApps();
    }

    /// <summary>Edit-flow constructor: always manual mode, prefilled.</summary>
    public AppEditDialogViewModel(AppEntry editing)
    {
        _allInstalledApps = [];
        IsEditing = true;
        IsManualMode = true;
        _manualName = editing.Name;
        _manualPath = editing.ExecutablePath;
        _manualArguments = editing.Arguments ?? string.Empty;
    }

    partial void OnSearchTextChanged(string value) => UpdateFilteredApps();

    private void UpdateFilteredApps()
    {
        FilteredApps.Clear();
        var query = SearchText.Trim();
        var matches = query.Length == 0
            ? _allInstalledApps
            : _allInstalledApps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var app in matches.Take(50))
            FilteredApps.Add(app);
    }

    [RelayCommand]
    private void PickInstalledApp(InstalledApp app)
    {
        Result = (app.Name, app.ExecutablePath, null);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SwitchToManual() => IsManualMode = true;

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe",
            Title = "Choose an application",
        };
        if (dialog.ShowDialog() != true) return;

        ManualPath = dialog.FileName;
        if (string.IsNullOrWhiteSpace(ManualName))
            ManualName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    partial void OnManualNameChanged(string value) =>
        NameError = AppEntryValidator.ValidateName(value) is { IsValid: false } r && value.Length > 0 ? r.ErrorMessage : null;

    partial void OnManualPathChanged(string value) =>
        PathError = AppEntryValidator.ValidateExecutablePath(value, File.Exists) is { IsValid: false } r && value.Length > 0 ? r.ErrorMessage : null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Result = (ManualName.Trim(), ManualPath, string.IsNullOrWhiteSpace(ManualArguments) ? null : ManualArguments);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSave() =>
        AppEntryValidator.ValidateName(ManualName).IsValid &&
        AppEntryValidator.ValidateExecutablePath(ManualPath, File.Exists).IsValid;
}
