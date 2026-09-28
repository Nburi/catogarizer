using System.Collections.Generic;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

/// <summary>A window in the active category's session.</summary>
public sealed record WindowRow(string Title, string AppName, int ProcessId)
{
    /// <summary>Null when the title already says it ("Arc", "Arc"), so the name isn't shown twice.</summary>
    public string? AppLabel => string.Equals(Title.Trim(), AppName.Trim(), StringComparison.OrdinalIgnoreCase) ? null : AppName;

    // List items are announced by ToString() - a record's default would read out every field.
    public override string ToString() => AppLabel is null ? Title : $"{Title}, {AppName}";
}

/// <summary>
/// An app the active category holds back. The icon comes from <see cref="IconPath"/> when the entry
/// is a full path, otherwise from a running process of that name (<see cref="IconProcessId"/>), if any.
/// </summary>
public sealed record HeldBackRow(string Name, string? IconPath, int IconProcessId)
{
    public override string ToString() => Name;
}

public sealed record PinnedRow(Guid Id, string Name, string? IconPath, int IconProcessId)
{
    public override string ToString() => Name;
}

public sealed record TemplateIcon(string Name, string ExecutablePath)
{
    public override string ToString() => Name;
}

/// <summary>A category on the shelf (every category except the active one).</summary>
public sealed partial class ShelfItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string? KeyText { get; init; }
    public required Brush Color { get; init; }
    public required bool IsUnsorted { get; init; }
    public required IReadOnlyList<TemplateIcon> Icons { get; init; }
    public required string? MoreIconsText { get; init; }
    public required string StateText { get; init; }
    public required bool HasParkedWindows { get; init; }
    public required int BlockedCount { get; init; }

    public bool HasBlocked => BlockedCount > 0;
    public string BlockedTooltip => BlockedCount == 1 ? "Holds back 1 app while active" : $"Holds back {BlockedCount} apps while active";
    public string AutomationName => $"Switch to {Name}, {StateText}";

    public override string ToString() => AutomationName;

    /// <summary>True while a template launch for this category is running (a restore is instant and never sets it).</summary>
    [ObservableProperty]
    private bool _isSwitching;
}
