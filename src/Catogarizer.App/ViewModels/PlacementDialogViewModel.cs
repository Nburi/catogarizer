using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

/// <summary>
/// The "grab, don't type" placement capture flow: open the app, let the
/// user drag/resize its real window, then read back its current bounds
/// instead of asking for typed coordinates.
/// </summary>
public partial class PlacementDialogViewModel : ObservableObject
{
    private readonly AppEntry _app;
    private readonly IProcessLauncher _launcher;
    private readonly IWindowFinder _windowFinder;
    private readonly IWindowManager _windowManager;
    private readonly IMonitorService _monitorService;

    private IntPtr? _hwnd;

    [ObservableProperty]
    private string _statusMessage = "Click \"Open app\" to launch it, then drag/resize its window where you want it.";

    [ObservableProperty]
    private string? _capturedSummary;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CapturePositionCommand))]
    private bool _canCapture;

    /// <summary>The placement to save when the dialog closes with Save - null means "no saved placement".</summary>
    public WindowRect? Result { get; private set; }

    public event EventHandler? RequestClose;

    public PlacementDialogViewModel(AppEntry app, IProcessLauncher launcher, IWindowFinder windowFinder,
        IWindowManager windowManager, IMonitorService monitorService)
    {
        _app = app;
        _launcher = launcher;
        _windowFinder = windowFinder;
        _windowManager = windowManager;
        _monitorService = monitorService;

        Result = app.Placement;
        if (app.Placement is not null)
            CapturedSummary = Describe(app.Placement);
    }

    [RelayCommand]
    private void OpenApp()
    {
        try
        {
            var pid = _launcher.Launch(_app.ExecutablePath, _app.Arguments);
            var hwnd = _windowFinder.FindMainWindow(pid, [_app.Name], TimeSpan.FromSeconds(6));
            if (hwnd is null)
            {
                StatusMessage = "Couldn't find the app's window. Make sure it opened, then try again.";
                return;
            }
            _hwnd = hwnd;
            CanCapture = true;
            StatusMessage = "Drag/resize the window where you want it, then click \"Capture position\".";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't launch the app: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private void CapturePosition()
    {
        if (_hwnd is not { } hwnd || !_windowManager.IsWindowOpen(hwnd))
        {
            StatusMessage = "The app's window isn't open anymore. Click \"Open app\" again.";
            CanCapture = false;
            return;
        }

        var bounds = _windowManager.GetBounds(hwnd);
        var monitors = _monitorService.GetMonitors();
        var placement = WindowPlacementResolver.CaptureFromBounds(bounds, monitors, _monitorService.GetPrimaryMonitor());

        Result = placement;
        CapturedSummary = Describe(placement);
        StatusMessage = "Captured. Click Save to keep it.";
    }

    [RelayCommand]
    private void ClearPlacement()
    {
        Result = null;
        CapturedSummary = null;
        StatusMessage = "Placement cleared - this app will open at its own default position.";
    }

    [RelayCommand]
    private void Save() => RequestClose?.Invoke(this, EventArgs.Empty);

    private static string Describe(WindowRect r) => $"{r.Width}×{r.Height}, offset ({r.OffsetX},{r.OffsetY}) from monitor top-left";
}
