using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = true;
    }

    private void HotkeyCaptureBox_GotFocus(object sender, RoutedEventArgs e)
    {
        HotkeyCaptureBox.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void HotkeyCaptureBox_LostFocus(object sender, RoutedEventArgs e)
    {
        HotkeyCaptureBox.BorderBrush = (Brush)FindResource("BorderBrush2");
    }

    private void HotkeyCaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key)) return; // wait for a real, non-modifier key

        var parts = new System.Collections.Generic.List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        if (parts.Count == 0) return; // require at least one modifier, to avoid claiming a plain letter key

        parts.Add(key.ToString());
        ViewModel.SetCapturedHotkey(string.Join("+", parts));
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;
}
