using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public sealed partial class DayOfWeekOption : ObservableObject
{
    public DayOfWeek Day { get; }
    public string Label => Day.ToString()[..3];

    [ObservableProperty]
    private bool _isSelected;

    public DayOfWeekOption(DayOfWeek day, bool isSelected)
    {
        Day = day;
        _isSelected = isSelected;
    }
}
