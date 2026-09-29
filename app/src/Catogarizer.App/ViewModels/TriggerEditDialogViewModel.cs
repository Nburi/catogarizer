using System.Collections.ObjectModel;
using System.Linq;
using Catogarizer.App.Services;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public sealed record TriggerTypeOption(TriggerType Value, string Label);

public sealed partial class TriggerEditDialogViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IInstalledAppFinder _installedAppFinder;
    private readonly IDialogService _dialogService;
    private readonly Trigger? _editing;

    public ObservableCollection<AppEntry> AvailableApps { get; }

    // Instance property (not static) - see the same note in TriggerActionEditItem.
    public IReadOnlyList<TriggerTypeOption> TriggerTypeOptions { get; } =
    [
        new(TriggerType.Startup, "Windows start"),
        new(TriggerType.Time, "Time of day"),
        new(TriggerType.Manual, "Manual only"),
    ];

    public string HeadingText => _editing is null ? "New trigger" : "Edit trigger";

    public ObservableCollection<DayOfWeekOption> DayOptions { get; }
    public ObservableCollection<TriggerActionEditItem> Actions { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(IsTimeType))]
    private TriggerType _type;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _timeOfDay = "08:00";

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _timeError;

    public bool IsTimeType => Type == TriggerType.Time;

    public event EventHandler? RequestClose;

    public TriggerEditDialogViewModel(LibraryService library, IInstalledAppFinder installedAppFinder, IDialogService dialogService, Trigger? editing = null)
    {
        _library = library;
        _installedAppFinder = installedAppFinder;
        _dialogService = dialogService;
        _editing = editing;
        _type = editing?.Type ?? TriggerType.Manual;

        AvailableApps = new ObservableCollection<AppEntry>(_library.Apps);
        DayOptions = new ObservableCollection<DayOfWeekOption>(
            Enum.GetValues<DayOfWeek>().Select(d => new DayOfWeekOption(d, editing?.DaysOfWeek.Contains(d) ?? false)));

        if (editing is null) return;

        _name = editing.Name;
        _timeOfDay = editing.TimeOfDay ?? "08:00";
        foreach (var action in editing.Actions)
            Actions.Add(new TriggerActionEditItem(_library, _installedAppFinder, _dialogService, _library.Categories, AvailableApps, action));
    }

    [RelayCommand]
    private void AddAction() =>
        Actions.Add(new TriggerActionEditItem(_library, _installedAppFinder, _dialogService, _library.Categories, AvailableApps));

    [RelayCommand]
    private void RemoveAction(TriggerActionEditItem item) => Actions.Remove(item);

    [RelayCommand]
    private void MoveActionUp(TriggerActionEditItem item)
    {
        var index = Actions.IndexOf(item);
        if (index > 0) Actions.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveActionDown(TriggerActionEditItem item)
    {
        var index = Actions.IndexOf(item);
        if (index >= 0 && index < Actions.Count - 1) Actions.Move(index, index + 1);
    }

    partial void OnNameChanged(string value) =>
        NameError = TriggerValidator.ValidateName(value, _library.Triggers, _editing?.Id) is { IsValid: false } r && value.Length > 0 ? r.ErrorMessage : null;

    partial void OnTimeOfDayChanged(string value) =>
        TimeError = TriggerValidator.ValidateTimeOfDay(Type, value) is { IsValid: false } r && value.Length > 0 ? r.ErrorMessage : null;

    partial void OnTypeChanged(TriggerType value) =>
        TimeError = TriggerValidator.ValidateTimeOfDay(value, TimeOfDay) is { IsValid: false } r ? r.ErrorMessage : null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var days = DayOptions.Where(d => d.IsSelected).Select(d => d.Day).ToList();
        var actions = Actions.Select(a => a.ToTriggerAction()).ToList();
        var timeOfDay = Type == TriggerType.Time ? TimeOfDay : null;

        if (_editing is null)
        {
            var trigger = _library.AddTrigger(Name, Type);
            _library.UpdateTrigger(trigger.Id, Name, Type, timeOfDay, days, actions);
        }
        else
        {
            _library.UpdateTrigger(_editing.Id, Name, Type, timeOfDay, days, actions);
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);

    private bool CanSave() =>
        TriggerValidator.ValidateName(Name, _library.Triggers, _editing?.Id).IsValid &&
        TriggerValidator.ValidateTimeOfDay(Type, TimeOfDay).IsValid;
}
