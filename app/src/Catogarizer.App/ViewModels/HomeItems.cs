using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Catogarizer.App.Services;
using Catogarizer.Core.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

/// <summary>A window in the active category's session.</summary>
public sealed record WindowRow(string Title, string AppName, int ProcessId, IntPtr Handle, string ProcessName)
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

/// <summary>
/// One line of the window context menu on the home. <see cref="Kind"/> is "item", "header" or
/// "separator"; headers and separators are not clickable. Compares by value.
/// </summary>
public abstract record WindowMenuEntry(string Text)
{
    public virtual string Kind => "item";

    /// <summary>Colour dot in front of the text, or null for none.</summary>
    public virtual Brush? Dot => null;

    public bool HasDot => Dot is not null;
    public override string ToString() => Text;
}

/// <summary>A place a window can be sent: another category or Unsorted.</summary>
public sealed record MoveTarget(Guid Id, string Name, System.Windows.Media.Color Color) : WindowMenuEntry(Name)
{
    public override Brush? Dot => ThemeService.FrozenBrush(Color);
}

/// <summary>"Pin app" or "Unpin app" for the window's app, depending on whether it is pinned right now.</summary>
public sealed record PinMenuEntry(string Text) : WindowMenuEntry(Text);

/// <summary>"Allow freezing while parked" or "Don't freeze this app", depending on whether the user allowed it already.</summary>
public sealed record FreezeMenuEntry(string Text) : WindowMenuEntry(Text);

public sealed record MenuHeaderEntry(string Text) : WindowMenuEntry(Text)
{
    public override string Kind => "header";
}

public sealed record MenuSeparatorEntry() : WindowMenuEntry("")
{
    public override string Kind => "separator";
}

public sealed record TemplateIcon(string Name, string ExecutablePath)
{
    public override string ToString() => Name;
}

/// <summary>One category in the Resources card: what it costs over its whole process tree.</summary>
public sealed record ResourceRow(string Name, System.Windows.Media.Color Hue, string StateText, ResourceLevel Level, string BytesText, string CpuText, double SharePercent,
    bool HeldBySound = false)
{
    /// <summary>Built from <see cref="Hue"/> so two rows with the same colour are equal and an unchanged card is not rebuilt.</summary>
    public Brush Color => ThemeService.FrozenBrush(Hue);

    public override string ToString() => $"{Name}, {StateText}{(HeldBySound ? ", plays sound" : "")}, {BytesText}, {CpuText} CPU";
}

/// <summary>A category on the shelf (every category except the active one).</summary>
public sealed partial class ShelfItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string? KeyText { get; init; }
    public required Brush Color { get; init; }
    public required bool IsUnsorted { get; init; }

    /// <summary>A quick category: shown with a TEMP chip instead of the editor, since there is nothing to edit.</summary>
    public required bool IsTemporary { get; init; }
    public required IReadOnlyList<TemplateIcon> Icons { get; init; }
    public required string? MoreIconsText { get; init; }
    public required string StateText { get; init; }
    public required bool HasParkedWindows { get; init; }
    public required int BlockedCount { get; init; }
    public required int TemplateCount { get; init; }

    /// <summary>An app of its template no longer exists at its path - switching would fail for it.</summary>
    public required bool HasMissingApp { get; init; }

    /// <summary>
    /// How much this category's apps have been put to rest, and what they hold in memory. Set in place by the
    /// home when the governor reports a change, so the tile can animate falling asleep instead of being rebuilt.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResting), nameof(LevelKind), nameof(LevelText), nameof(LevelTooltip), nameof(AutomationName))]
    private ResourceLevel _level;

    /// <summary>Memory of the category's whole process tree; 0 while unknown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMemory), nameof(AutomationName))]
    private long _memoryBytes;

    /// <summary>A parked tree of this category is left at full speed only because it plays sound (the badge on the tile).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    private bool _holdsAwakeBySound;

    /// <summary>Unsorted and quick categories have no editor, so the way to change it is only named where there is one.</summary>
    public string SoundTooltip => CanEdit ? "Playing sound, so it stays awake. Change this in the category's editor." : "Playing sound, so it stays awake.";

    public bool IsResting => Level >= ResourceLevel.Eco;
    public bool HasMemory => MemoryBytes > 0;

    /// <summary>"Eco", "Asleep" or "Frozen" - the glyph the tile shows; empty when nothing was done.</summary>
    public string LevelKind => Level switch
    {
        ResourceLevel.Eco => "Eco",
        ResourceLevel.Asleep => "Asleep",
        ResourceLevel.Frozen => "Frozen",
        _ => "",
    };

    public string? LevelText => LevelKind.Length == 0 ? null : LevelKind;

    public string? LevelTooltip => Level switch
    {
        ResourceLevel.Eco => "Slowed down while parked. Back to full speed when you switch here.",
        ResourceLevel.Asleep => "Memory paged out to make room. Wakes when you switch here.",
        ResourceLevel.Frozen => "Paused because you allowed it. Wakes when you switch here.",
        _ => null,
    };

    /// <summary>Everything that is rebuilt only when it changes: the resource figures are left out on purpose.</summary>
    public bool SameEntryAs(ShelfItem other) =>
        Id == other.Id && Name == other.Name && KeyText == other.KeyText && IsUnsorted == other.IsUnsorted
        && IsTemporary == other.IsTemporary && MoreIconsText == other.MoreIconsText && StateText == other.StateText
        && HasParkedWindows == other.HasParkedWindows && BlockedCount == other.BlockedCount
        && TemplateCount == other.TemplateCount && HasMissingApp == other.HasMissingApp
        && Icons.SequenceEqual(other.Icons) && SameColor(Color, other.Color);

    private static bool SameColor(Brush a, Brush b) =>
        a is SolidColorBrush x && b is SolidColorBrush y ? x.Color == y.Color : ReferenceEquals(a, b);

    public bool CanEdit => !IsUnsorted && !IsTemporary;

    /// <summary>Everything but Unsorted can be closed: its windows are asked to close.</summary>
    public bool CanClose => !IsUnsorted;
    public bool HasBlocked => BlockedCount > 0;
    public string BlockedTooltip => BlockedCount == 1 ? "Holds back 1 app while active" : $"Holds back {BlockedCount} apps while active";
    public string AutomationName
    {
        get
        {
            var resources = (LevelText is { } level ? $", {level.ToLowerInvariant()}" : "") + (HasMemory ? $", {ByteSize.Format(MemoryBytes)}" : "")
                + (HoldsAwakeBySound ? ", plays sound and stays awake" : "");
            return BlockedCount switch
            {
                0 => $"Switch to {Name}{(IsTemporary ? " (temporary)" : "")}, {StateText}{resources}",
                1 => $"Switch to {Name}, {StateText}{resources}, holds back 1 app",
                _ => $"Switch to {Name}, {StateText}{resources}, holds back {BlockedCount} apps",
            };
        }
    }

    public override string ToString() => AutomationName;

    /// <summary>True while a template launch for this category is running (a restore is instant and never sets it).</summary>
    [ObservableProperty]
    private bool _isSwitching;
}
