using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class CommandPaletteWindow : Window
{
    // Several paths ask to close (Esc, Deactivated, a finished switch, the hotkey); once one
    // has started closing, the rest must be no-ops - WPF throws on Close/Show during closing.
    private bool _isClosing;

    public CommandPaletteViewModel ViewModel { get; }

    public bool IsOpen => IsVisible && !_isClosing;

    public CommandPaletteWindow(CommandPaletteViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Closing += (_, _) => _isClosing = true;
        viewModel.RequestClose += (_, _) => Dismiss();

        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = SystemParameters.PrimaryScreenHeight * 0.2;

        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            FadeIn();
        };
    }

    public void Dismiss()
    {
        if (_isClosing) return;
        _isClosing = true;
        Close();
    }

    private void FadeIn()
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(140));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        if (SystemParameters.ClientAreaAnimation)
            EntryOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, 0, duration) { EasingFunction = ease });
        else
            EntryOffset.Y = 0;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && ViewModel.Results.Count > 0)
        {
            ViewModel.SwitchToCommand.Execute(ViewModel.Results[0]);
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Dismiss();
}
