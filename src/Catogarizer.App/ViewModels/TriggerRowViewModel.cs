using System.Linq;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public sealed partial class TriggerRowViewModel : ObservableObject
{
    private readonly LibraryService _library;

    public Trigger Trigger { get; }

    public string Name => Trigger.Name;

    public string TypeLabel => Trigger.Type switch
    {
        TriggerType.Startup => "Windows start",
        TriggerType.Time => "Time",
        TriggerType.Manual => "Manual",
        _ => Trigger.Type.ToString(),
    };

    public string? ScheduleLabel => Trigger.Type == TriggerType.Time
        ? $"{Trigger.TimeOfDay} · {(Trigger.DaysOfWeek.Count == 0 ? "every day" : string.Join(", ", Trigger.DaysOfWeek.OrderBy(d => d).Select(d => d.ToString()[..3])))}"
        : null;

    public string ActionsSummary => Trigger.Actions.Count switch
    {
        0 => "No actions",
        1 => "1 action",
        _ => $"{Trigger.Actions.Count} actions",
    };

    [ObservableProperty]
    private bool _isEnabled;

    public TriggerRowViewModel(Trigger trigger, LibraryService library)
    {
        Trigger = trigger;
        _library = library;
        _isEnabled = trigger.IsEnabled;
    }

    /// <summary>Persists immediately on toggle - same pattern as SettingsViewModel's
    /// OnStartWithWindowsChanged, no separate "Save" step for this one setting.</summary>
    partial void OnIsEnabledChanged(bool value) => _library.SetTriggerEnabled(Trigger.Id, value);
}
