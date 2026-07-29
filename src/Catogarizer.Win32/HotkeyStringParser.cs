using Catogarizer.Core.Services;

namespace Catogarizer.Win32;

/// <summary>Parses a hotkey setting like "Ctrl+Alt+Space" into (modifiers, virtual-key code).</summary>
public static class HotkeyStringParser
{
    private static readonly Dictionary<string, int> KeyNameToVk = BuildKeyMap();

    public static bool TryParse(string text, out HotkeyModifiers modifiers, out int virtualKeyCode)
    {
        modifiers = HotkeyModifiers.None;
        virtualKeyCode = 0;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Control,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" or "windows" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };
        }

        var keyName = parts[^1];
        if (!KeyNameToVk.TryGetValue(keyName.ToLowerInvariant(), out virtualKeyCode)) return false;

        return modifiers != HotkeyModifiers.None;
    }

    private static Dictionary<string, int> BuildKeyMap()
    {
        var map = new Dictionary<string, int> { ["space"] = 0x20 };
        for (var c = 'a'; c <= 'z'; c++) map[c.ToString()] = char.ToUpperInvariant(c);
        for (var d = '0'; d <= '9'; d++) map[d.ToString()] = d;
        for (var f = 1; f <= 12; f++) map[$"f{f}"] = 0x70 + (f - 1);
        return map;
    }
}
