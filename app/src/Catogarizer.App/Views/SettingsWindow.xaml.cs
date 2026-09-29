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
        Closed += (_, _) => viewModel.RevertUnsavedTheme();
    }

    private void ThemeTile_Loaded(object sender, RoutedEventArgs e)
    {
        var tile = (System.Windows.Controls.RadioButton)sender;
        tile.IsChecked = ReferenceEquals(tile.DataContext, ViewModel.SelectedTheme);
    }

    private void ThemeTile_Checked(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ThemeOption option)
            ViewModel.SelectedTheme = option;
    }

    private void HotkeyCaptureBox_GotFocus(object sender, RoutedEventArgs e)
    {
        HotkeyCaptureBox.BorderBrush = (Brush)FindResource("AccentBrush");
        HotkeyHelp.Text = "Listening - press the combination now (Tab to skip).";
    }

    private void HotkeyCaptureBox_LostFocus(object sender, RoutedEventArgs e)
    {
        HotkeyCaptureBox.ClearValue(BorderBrushProperty);
        HotkeyHelp.Text = "Click the box above, then press the key combination you want.";
    }

    private void HotkeyCaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Tab and Escape keep working, so the field can't trap keyboard users.
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Tab or Key.Escape) return;
        if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Tab) return;
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
