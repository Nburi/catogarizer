using System.Windows.Input;

namespace Catogarizer.App.Views;

public static class KeyDigits
{
    /// <summary>The digit a number-row or numpad key stands for, or null for any other key.</summary>
    public static int? Of(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
    };
}
