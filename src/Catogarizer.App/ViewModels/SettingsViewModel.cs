using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IAutostartService _autostartService;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _hotkeyText;

    /// <summary>True once the persisted hotkey has actually changed - callers use this to
    /// decide whether the live global hotkey registration needs to be refreshed.</summary>
    public bool HotkeyChanged { get; private set; }

    public event EventHandler? RequestClose;

    public SettingsViewModel(LibraryService library, IAutostartService autostartService)
    {
        _library = library;
        _autostartService = autostartService;
        _startWithWindows = autostartService.IsEnabled;
        _startMinimized = library.Settings.StartMinimized;
        _hotkeyText = library.Settings.CommandPaletteHotkey;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (value) _autostartService.Enable();
        else _autostartService.Disable();
    }

    /// <summary>Called by the view's key-capture handler once a valid combination is pressed.</summary>
    public void SetCapturedHotkey(string hotkeyText) => HotkeyText = hotkeyText;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        HotkeyChanged = !string.Equals(_library.Settings.CommandPaletteHotkey, HotkeyText, StringComparison.OrdinalIgnoreCase);
        _library.UpdateSettings(StartMinimized, HotkeyText);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);

    private bool CanSave() => !string.IsNullOrWhiteSpace(HotkeyText);
}
