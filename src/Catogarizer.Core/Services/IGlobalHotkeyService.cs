namespace Catogarizer.Core.Services;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public interface IGlobalHotkeyService : IDisposable
{
    event Action? HotkeyPressed;

    /// <returns>false if the combination is already claimed by another app.</returns>
    bool Register(HotkeyModifiers modifiers, int virtualKeyCode);

    void Unregister();
}
