using System.Collections.ObjectModel;
using System.IO;
using Catogarizer.Core;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class AppEditDialogViewModel : ObservableObject
{
    private readonly IWindowEnumerator _windowEnumerator;
    private readonly IWindowManager _windowManager;

    public string Title { get; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string executablePath = string.Empty;

    [ObservableProperty]
    private string arguments = string.Empty;

    [ObservableProperty]
    private string? workingDirectory;

    [ObservableProperty]
    private int windowX;

    [ObservableProperty]
    private int windowY;

    [ObservableProperty]
    private int windowWidth = 1280;

    [ObservableProperty]
    private int windowHeight = 800;

    [ObservableProperty]
    private int launchDelayMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    private IReadOnlyList<string> errors = Array.Empty<string>();

    public bool HasErrors => Errors.Count > 0;

    public ObservableCollection<OpenWindowInfo> RunningWindows { get; } = new();

    [ObservableProperty]
    private OpenWindowInfo? selectedRunningWindow;

    public AppEditDialogViewModel(AppEntry? existing, IWindowEnumerator windowEnumerator, IWindowManager windowManager)
    {
        _windowEnumerator = windowEnumerator;
        _windowManager = windowManager;
        Title = existing is null ? "New App" : "Edit App";

        if (existing is not null)
        {
            name = existing.Name;
            executablePath = existing.ExecutablePath;
            arguments = existing.Arguments;
            workingDirectory = existing.WorkingDirectory;
            windowX = existing.Window.X;
            windowY = existing.Window.Y;
            windowWidth = existing.Window.Width;
            windowHeight = existing.Window.Height;
            launchDelayMs = existing.LaunchDelayMs;
        }

        RefreshRunningWindows();
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Choose an application",
        };
        if (dialog.ShowDialog() != true)
            return;

        ExecutablePath = dialog.FileName;
        if (string.IsNullOrWhiteSpace(Name))
            Name = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    [RelayCommand]
    private void RefreshRunningWindows()
    {
        var selectedHandle = SelectedRunningWindow?.Handle;
        RunningWindows.Clear();
        foreach (var window in _windowEnumerator.GetOpenWindows().OrderBy(w => w.Title))
            RunningWindows.Add(window);

        SelectedRunningWindow = RunningWindows.FirstOrDefault(w => w.Handle == selectedHandle);
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private void Capture()
    {
        if (SelectedRunningWindow is null)
            return;

        var rect = _windowManager.GetRect(SelectedRunningWindow.Handle);
        if (rect is null)
            return;

        WindowX = rect.X;
        WindowY = rect.Y;
        WindowWidth = rect.Width;
        WindowHeight = rect.Height;
    }

    private bool CanCapture() => SelectedRunningWindow is not null;

    partial void OnSelectedRunningWindowChanged(OpenWindowInfo? value) => CaptureCommand.NotifyCanExecuteChanged();

    public bool TryBuildEntry(Guid? existingId, int order, out AppEntry entry)
    {
        entry = new AppEntry
        {
            Id = existingId ?? Guid.NewGuid(),
            Name = Name.Trim(),
            ExecutablePath = ExecutablePath.Trim(),
            Arguments = Arguments.Trim(),
            WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim(),
            Window = new WindowRect { X = WindowX, Y = WindowY, Width = WindowWidth, Height = WindowHeight },
            LaunchDelayMs = LaunchDelayMs,
            Order = order,
        };

        Errors = Validation.ValidateAppEntry(entry);
        return Errors.Count == 0;
    }
}
