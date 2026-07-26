using System.Collections.ObjectModel;
using Catogarizer.App.Services;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class CategoryViewModel : ObservableObject
{
    private readonly ICategoryActionService _actionService;
    private readonly IProcessLauncher _launcher;
    private readonly IDialogService _dialogService;
    private readonly Func<Task> _persistAsync;

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

    public CategoryViewModel(
        Category model,
        ICategoryActionService actionService,
        IProcessLauncher launcher,
        IDialogService dialogService,
        Func<Task> persistAsync)
    {
        Model = model;
        _actionService = actionService;
        _launcher = launcher;
        _dialogService = dialogService;
        _persistAsync = persistAsync;

        Apps = new ObservableCollection<AppTileViewModel>(
            model.Apps.OrderBy(a => a.Order).Select(CreateAppTileViewModel));
        Apps.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasApps));

        RefreshRunningStatus();
    }

    public void NotifyNameChanged() => OnPropertyChanged(nameof(Name));

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task OpenAsync() => RunActionAsync(_actionService.OpenAsync, "opened");

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task CloseAsync() => RunActionAsync(_actionService.CloseAsync, "closed");

    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private Task MinimizeAsync() => RunActionAsync(_actionService.MinimizeAsync, "minimized");

    [RelayCommand]
    private async Task AddAppAsync()
    {
        var entry = _dialogService.ShowAppEdit(null, Model.Apps.Count);
        if (entry is null)
            return;

        Model.Apps.Add(entry);
        Apps.Add(CreateAppTileViewModel(entry));
        RefreshRunningStatus();
        await _persistAsync();
    }

    [RelayCommand]
    private async Task EditAppAsync(AppTileViewModel? tile)
    {
        if (tile is null)
            return;

        var updated = _dialogService.ShowAppEdit(tile.Model, tile.Model.Order);
        if (updated is null)
            return;

        var modelIndex = Model.Apps.FindIndex(a => a.Id == tile.Model.Id);
        if (modelIndex >= 0)
            Model.Apps[modelIndex] = updated;

        var tileIndex = Apps.IndexOf(tile);
        if (tileIndex >= 0)
            Apps[tileIndex] = CreateAppTileViewModel(updated);

        RefreshRunningStatus();
        await _persistAsync();
    }

    [RelayCommand]
    private async Task DeleteAppAsync(AppTileViewModel? tile)
    {
        if (tile is null)
            return;

        var confirmed = _dialogService.ShowConfirm("Delete app", $"Remove \"{tile.Name}\" from this category?");
        if (!confirmed)
            return;

        Model.Apps.Remove(tile.Model);
        Apps.Remove(tile);
        await _persistAsync();
    }

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

    private AppTileViewModel CreateAppTileViewModel(AppEntry entry) =>
        new(entry, _actionService, _launcher);

    private void RefreshRunningStatus()
    {
        foreach (var tile in Apps)
            tile.RefreshRunningState();
    }
}
