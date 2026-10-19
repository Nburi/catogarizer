namespace Catogarizer.Core.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public string CommandPaletteHotkey { get; set; } = "Ctrl+Alt+Space";
    /// <summary>Slow parked apps down, and free memory when it gets tight (CONCEPT.md "Resource saving").</summary>
    public bool SaveResources { get; set; } = true;
    public string Theme { get; set; } = Theming.ThemeCatalog.DefaultId;
}
