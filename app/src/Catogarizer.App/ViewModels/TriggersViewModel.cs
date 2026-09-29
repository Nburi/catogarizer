using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Catogarizer.App.Services;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed partial class TriggersViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IDialogService _dialogService;
    private readonly IInstalledAppFinder _installedAppFinder;
    private readonly TriggerRunner _triggerRunner;

    public ObservableCollection<TriggerRowViewModel> Triggers { get; } = new();

    public bool HasTriggers => Triggers.Count > 0;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private bool _noticeIsError;

    public TriggersViewModel(LibraryService library, IDialogService dialogService, IInstalledAppFinder installedAppFinder, TriggerRunner triggerRunner)
    {
        _library = library;
        _dialogService = dialogService;
        _installedAppFinder = installedAppFinder;
        _triggerRunner = triggerRunner;
        RefreshTriggers();
    }

    private void RefreshTriggers()
    {
        Triggers.Clear();
        foreach (var trigger in _library.Triggers)
            Triggers.Add(new TriggerRowViewModel(trigger, _library));
        OnPropertyChanged(nameof(HasTriggers));
    }

    [RelayCommand]
    private void AddTrigger()
    {
        var vm = new TriggerEditDialogViewModel(_library, _installedAppFinder, _dialogService);
        if (_dialogService.ShowTriggerEdit(vm))
            RefreshTriggers();
    }

    [RelayCommand]
    private void EditTrigger(TriggerRowViewModel row)
    {
        var vm = new TriggerEditDialogViewModel(_library, _installedAppFinder, _dialogService, row.Trigger);
        if (_dialogService.ShowTriggerEdit(vm))
            RefreshTriggers();
    }

    [RelayCommand]
    private void DeleteTrigger(TriggerRowViewModel row)
    {
        var confirmed = _dialogService.ShowConfirm("Delete trigger?", $"\"{row.Trigger.Name}\" will be removed.");
        if (!confirmed) return;

        _library.DeleteTrigger(row.Trigger.Id);
        RefreshTriggers();
    }

    [RelayCommand]
    private async Task RunNow(TriggerRowViewModel row)
    {
        if (!row.HasActions)
        {
            Notice = "This trigger has no actions yet. Edit it to add some.";
            NoticeIsError = false;
            return;
        }
        IsBusy = true;
        BusyMessage = $"Running \"{row.Trigger.Name}\"...";
        Notice = null;
        try
        {
            var results = await Task.Run(() => _triggerRunner.Run(row.Trigger, _library));
            var failures = results.Where(r => r.Outcome == AppActionOutcome.Failed).ToList();
            if (failures.Count > 0)
            {
                Notice = failures.Count == 1
                    ? failures[0].ErrorMessage
                    : $"{failures.Count} apps had a problem: {string.Join(" ", failures.Select(f => f.ErrorMessage))}";
                NoticeIsError = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;
}
