using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Catogarizer.App.Services;
using Catogarizer.Core.Services;
using Catogarizer.Core.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed record ThemeOption(string Id, string Name, string Description, Brush Background, Brush Surface, Brush Accent, Brush Ink)
{
    public override string ToString() => $"{Name}, {Description}";
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IAutostartService _autostartService;
    private readonly ThemeService _themeService;
    private readonly string _savedThemeId;
    private bool _isSaved;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _hotkeyText;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    public IReadOnlyList<ThemeOption> Themes { get; }

    /// <summary>True once the persisted hotkey has actually changed - callers use this to
    /// decide whether the live global hotkey registration needs to be refreshed.</summary>
    public bool HotkeyChanged { get; private set; }

    public event EventHandler? RequestClose;

    /// <summary>Raised by the "Automation..." button - handled by MainViewModel, which owns
    /// the dependencies (LibraryService, TriggerRunner) needed to open the Triggers window.
    /// Doesn't close this settings dialog; the Triggers window opens as a further nested
    /// modal on top of it, same owner (MainWindow), which WPF supports fine.</summary>
    public event EventHandler? RequestOpenTriggers;

    private readonly Action _showAllHiddenWindows;

    [ObservableProperty] private string? _showAllDoneText;

    public SettingsViewModel(LibraryService library, IAutostartService autostartService, ThemeService themeService, Action showAllHiddenWindows)
    {
        _showAllHiddenWindows = showAllHiddenWindows;
        _library = library;
        _autostartService = autostartService;
        _themeService = themeService;
        _startWithWindows = autostartService.IsEnabled;
        _startMinimized = library.Settings.StartMinimized;
        _hotkeyText = library.Settings.CommandPaletteHotkey;

        Themes = ThemeCatalog.All.Select(t => new ThemeOption(t.Id, t.Name, t.Description,
            Frozen(t.Bg), Frozen(t.Surface), Frozen(t.Accent), Frozen(t.Ink))).ToList();
        _savedThemeId = themeService.Current.Id;
        _selectedTheme = Themes.First(t => t.Id == _savedThemeId);
    }

    private static Brush Frozen(Oklch color)
    {
        var brush = new SolidColorBrush(ThemeService.ToColor(color.ToRgb()));
        brush.Freeze();
        return brush;
    }

    /// <summary>Live preview: the whole app switches as soon as a theme is picked.</summary>
    partial void OnSelectedThemeChanged(ThemeOption value) => _themeService.Apply(value.Id);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (value) _autostartService.Enable();
        else _autostartService.Disable();
    }

    /// <summary>Called by the view's key-capture handler once a valid combination is pressed.</summary>
    public void SetCapturedHotkey(string hotkeyText) => HotkeyText = hotkeyText;

    /// <summary>Called when the dialog closes any way other than Save (Cancel, X, Esc).</summary>
    public void RevertUnsavedTheme()
    {
        if (!_isSaved && _themeService.Current.Id != _savedThemeId)
            _themeService.Apply(_savedThemeId);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        HotkeyChanged = !string.Equals(_library.Settings.CommandPaletteHotkey, HotkeyText, StringComparison.OrdinalIgnoreCase);
        _library.UpdateSettings(StartMinimized, HotkeyText);
        _library.SetTheme(SelectedTheme.Id);
        _isSaved = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        RevertUnsavedTheme();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenTriggers() => RequestOpenTriggers?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async System.Threading.Tasks.Task ShowAllHiddenWindowsAsync()
    {
        await System.Threading.Tasks.Task.Run(_showAllHiddenWindows);
        ShowAllDoneText = "Done. Every parked window is visible again, and you're in Unsorted.";
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(HotkeyText);
}
