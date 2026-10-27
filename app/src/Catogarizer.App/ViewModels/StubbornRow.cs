using System.Linq;
using Catogarizer.App.Services;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

/// <summary>
/// One program that kept a window open after "Close windows", in the banner under the close report. It says why it
/// stayed, and ending it is two steps in the same row: "End", then "End program" with what that costs - no dialog.
/// </summary>
public sealed partial class StubbornRow : ObservableObject
{
    private readonly string _name;

    public StubbornRow(StubbornProgram program)
    {
        _program = program;
        // Read once: the name comes from the running program, and it is gone when it has been ended.
        _name = AppNames.ForProcess(program.ProcessId, program.ProcessName);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowsText), nameof(ReasonText), nameof(ConfirmText), nameof(DetailText), nameof(CanEnd),
        nameof(ShowPrimary), nameof(ShowSecondary), nameof(ShowsEnd))]
    private StubbornProgram _program;

    /// <summary>"Ending..." while the process is being killed; the row's buttons are off then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText), nameof(CanAct))]
    private bool _isEnding;

    /// <summary>The first click was made: the row asks for the second.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText), nameof(ShowPrimary), nameof(ShowSecondary), nameof(ShowsEnd))]
    private bool _isConfirming;

    /// <summary>What went wrong, when ending did not work. Cleared by the next attempt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private string? _resultText;

    /// <summary>The program is gone: the row fades out before it is removed.</summary>
    [ObservableProperty] private bool _isDissolving;

    public string Name => _name;
    public int ProcessId => Program.ProcessId;
    public string WindowsText => Program.Windows.Count == 1 ? "1 window" : $"{Program.Windows.Count} windows";
    public bool CanEnd => !Program.IsProtected;
    /// <summary>Off while the process is being ended, so a second click cannot start a second attempt.</summary>
    public bool CanAct => !IsEnding;

    /// <summary>"Show" leads when the program is probably waiting for an answer; ending it would lose that work.</summary>
    public bool ShowPrimary => !IsConfirming && Program.Reason == StayReason.AsksYou;
    public bool ShowSecondary => !IsConfirming && Program.Reason != StayReason.AsksYou;
    public bool ShowsEnd => !IsConfirming && CanEnd;

    public string ReasonText => !CanEnd
        ? "Part of Windows or Catogarizer, so it is never ended from here."
        : Program.Reason switch
        {
            StayReason.AsksYou => "Probably asking you to save. Show it to answer.",
            StayReason.StillOpen => "Doesn't react to the close request.",
            _ => "Keeps running in the background, like a tray app.",
        };

    /// <summary>What the second click does - every cost, stated before it happens.</summary>
    public string ConfirmText
    {
        get
        {
            var text = $"Ends {Name} right away. Unsaved work in it is lost.";
            if (Program.Windows.Count > 1) text += $" All its {Program.Windows.Count} windows close.";
            if (Program.Shared.Count > 0)
            {
                var total = Program.Shared.Sum(s => s.Count);
                var where = string.Join(", ", Program.Shared.Select(s => s.CategoryName));
                text += $" Also closes {(total == 1 ? "1 window" : $"{total} windows")} in {where}.";
            }
            return text;
        }
    }

    public string DetailText => ResultText ?? (IsEnding ? "Ending..." : IsConfirming ? ConfirmText : ReasonText);

    public override string ToString() => $"{Name}, {WindowsText}, {ReasonText}";
}
