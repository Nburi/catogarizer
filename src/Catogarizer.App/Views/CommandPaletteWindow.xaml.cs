using System.ComponentModel;
using System.Linq;
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
            Dispatcher.BeginInvoke(RevealSelection, System.Windows.Threading.DispatcherPriority.Loaded);
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

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Dismiss();
                return;
            case Key.Down:
                e.Handled = true;
                ViewModel.MoveSelection(+1);
                RevealSelection();
                return;
            case Key.Up:
                e.Handled = true;
                ViewModel.MoveSelection(-1);
                RevealSelection();
                return;
            case Key.Enter:
                e.Handled = true;
                await ViewModel.SwitchSelectedAsync();
                return;
        }

        // Digits jump straight to a category - but only before anything is typed, so a
        // category name with a number in it can still be searched.
        if (ViewModel.SearchText.Length == 0 && Keyboard.Modifiers == ModifierKeys.None)
        {
            var number = e.Key switch
            {
                >= Key.D0 and <= Key.D9 => e.Key - Key.D0,
                >= Key.NumPad0 and <= Key.NumPad9 => e.Key - Key.NumPad0,
                _ => -1,
            };
            if (number >= 0)
            {
                e.Handled = true;
                await ViewModel.SwitchByNumberAsync(number);
            }
        }
    }

    /// <summary>With many categories the list scrolls; the keyboard selection must never be off-screen.</summary>
    private void RevealSelection()
    {
        var selected = ViewModel.Results.FirstOrDefault(r => r.IsSelected);
        if (selected is not null && ResultsList.ItemContainerGenerator.ContainerFromItem(selected) is FrameworkElement row)
            row.BringIntoView();
    }

    private void Row_MouseEnter(object sender, MouseEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PaletteEntry hovered) return;
        foreach (var entry in ViewModel.Results) entry.IsSelected = ReferenceEquals(entry, hovered);
    }

    private void Window_Deactivated(object sender, EventArgs e) => Dismiss();
}
