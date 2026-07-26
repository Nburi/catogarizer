using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAutostartManager _autostartManager;
    private readonly Func<Task> _persistAsync;

    [ObservableProperty]
    private bool autostartEnabled;

    [ObservableProperty]
    private bool startMinimizedToTray;

    public SettingsViewModel(AppSettings settings, IAutostartManager autostartManager, Func<Task> persistAsync)
    {
        _settings = settings;
        _autostartManager = autostartManager;
        _persistAsync = persistAsync;
        autostartEnabled = settings.AutostartEnabled;
        startMinimizedToTray = settings.StartMinimizedToTray;
    }

    partial void OnAutostartEnabledChanged(bool value)
    {
        _settings.AutostartEnabled = value;
        if (value)
            _autostartManager.Enable();
        else
            _autostartManager.Disable();

        _ = _persistAsync();
    }

    partial void OnStartMinimizedToTrayChanged(bool value)
    {
        _settings.StartMinimizedToTray = value;
        _ = _persistAsync();
    }
}
