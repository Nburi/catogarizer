using System.Collections.ObjectModel;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class CategoryViewModel : ObservableObject
{
    private readonly ICategoryActionService _actionService;
    private readonly IProcessLauncher _launcher;

    public Category Model { get; }

    public Guid Id => Model.Id;
    public string Name => Model.Name;
    public ObservableCollection<AppTileViewModel> Apps { get; }

    public bool HasApps => Apps.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunActions))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    [NotifyCanExecuteChangedFor(nameof(MinimizeCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? statusMessage;

    [ObservableProperty]
    private bool statusIsError;

    public bool CanRunActions => !IsBusy;
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public CategoryViewModel(Category model, ICategoryActionService actionService, IProcessLauncher launcher)
    {
        Model = model;
        _actionService = actionService;
        _launcher = launcher;

        Apps = new ObservableCollection<AppTileViewModel>(
            model.Apps.OrderBy(a => a.Order).Select(a => new AppTileViewModel(a)));
        Apps.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasApps));

        RefreshRunningStatus();
    }

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task OpenAsync() => RunActionAsync(_actionService.OpenAsync, "opened");

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task CloseAsync() => RunActionAsync(_actionService.CloseAsync, "closed");

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task MinimizeAsync() => RunActionAsync(_actionService.MinimizeAsync, "minimized");

    private async Task RunActionAsync(
        Func<Category, CancellationToken, Task<CategoryActionResult>> action,
        string verb)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await action(Model, CancellationToken.None);
            RefreshRunningStatus();

            var failed = result.Outcomes.Where(o => !o.Success).ToList();
            if (failed.Count == 0)
            {
                StatusIsError = false;
                var count = result.Outcomes.Count;
                StatusMessage = count == 0
                    ? "No apps in this category."
                    : $"{count} app{(count == 1 ? "" : "s")} {verb}.";
            }
            else
            {
                StatusIsError = true;
                StatusMessage = $"{failed.Count} of {result.Outcomes.Count} failed: " +
                                 string.Join("; ", failed.Select(f => f.ErrorMessage));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshRunningStatus()
    {
        foreach (var tile in Apps)
            tile.IsRunning = _launcher.FindRunning(tile.Model) is not null;
    }
}
