using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class AppTileViewModel : ObservableObject
{
    private readonly ICategoryActionService _actionService;
    private readonly IProcessLauncher _launcher;

    public AppEntry Model { get; }

    public string Name => Model.Name;
    public string Initial => string.IsNullOrEmpty(Model.Name) ? "?" : Model.Name[..1].ToUpperInvariant();
    public ImageSource? IconImage { get; }
    public bool HasIcon => IconImage is not null;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunActions))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    [NotifyCanExecuteChangedFor(nameof(MinimizeCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    public bool CanRunActions => !IsBusy;

    public AppTileViewModel(AppEntry model, ICategoryActionService actionService, IProcessLauncher launcher, IAppIconProvider iconProvider)
    {
        Model = model;
        _actionService = actionService;
        _launcher = launcher;
        IsRunning = _launcher.FindRunning(Model) is not null;
        IconImage = LoadIcon(model.ExecutablePath, iconProvider);
    }

    private static ImageSource? LoadIcon(string executablePath, IAppIconProvider iconProvider)
    {
        var handle = iconProvider.GetIconHandle(executablePath);
        if (handle == 0)
            return null;

        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            iconProvider.ReleaseIconHandle(handle);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task OpenAsync() => RunActionAsync(_actionService.OpenAsync, "opened");

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task CloseAsync() => RunActionAsync(_actionService.CloseAsync, "closed");

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task MinimizeAsync() => RunActionAsync(_actionService.MinimizeAsync, "minimized");

    /// <summary>
    /// Runs a category-scoped action against a synthetic single-app category, reusing
    /// ICategoryActionService's tested launch/reposition/error-handling logic instead of
    /// duplicating it here for a "category of one".
    /// </summary>
    private async Task RunActionAsync(
        Func<Category, CancellationToken, Task<CategoryActionResult>> action,
        string verb)
    {
        IsBusy = true;
        try
        {
            var singleAppCategory = new Category { Apps = { Model } };
            var result = await action(singleAppCategory, CancellationToken.None);
            var outcome = result.Outcomes.Single();
            StatusMessage = outcome.Success ? $"{Name} {verb}." : outcome.ErrorMessage;
        }
        finally
        {
            IsRunning = _launcher.FindRunning(Model) is not null;
            IsBusy = false;
        }
    }

    public void RefreshRunningState() => IsRunning = _launcher.FindRunning(Model) is not null;
}
