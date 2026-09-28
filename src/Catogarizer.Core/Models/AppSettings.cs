namespace Catogarizer.Core.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public string CommandPaletteHotkey { get; set; } = "Ctrl+Alt+Space";
    public string Theme { get; set; } = Theming.ThemeCatalog.DefaultId;
}
