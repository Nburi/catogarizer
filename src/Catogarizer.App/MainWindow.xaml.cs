using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Services;

namespace Catogarizer.App;

public partial class MainWindow : Window
{
    private const double NarrowWidth = 860;

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _liveRefresh;

    public MainWindow(EditorServices services, CategorySwitcher switcher, ThemeService themeService,
        IAppBlockingService appBlockingService, Action<string> onHotkeyChanged)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(services, switcher, themeService, appBlockingService, onHotkeyChanged);
        DataContext = _viewModel;

        // Window titles and "48 min" change without any event - refresh while visible only.
        _liveRefresh = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => _viewModel.Refresh(), Dispatcher);
        _liveRefresh.Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { _viewModel.Refresh(); _liveRefresh.Start(); }
            else _liveRefresh.Stop();
        };
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox || Keyboard.Modifiers != ModifierKeys.None) return;
        var number = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => e.Key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => e.Key - Key.NumPad0,
            _ => -1,
        };
        if (number < 0) return;
        e.Handled = true;
        await _viewModel.SwitchByNumberAsync(number);
    }

    /// <summary>Narrow windows stack the side column under the windows list instead of squeezing both.</summary>
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = ActualWidth < NarrowWidth;
        Grid.SetColumn(SideColumn, narrow ? 0 : 2);
        Grid.SetRow(SideColumn, narrow ? 1 : 0);
        Grid.SetColumnSpan(SideColumn, narrow ? 3 : 1);
        Grid.SetColumnSpan(NowColumn, narrow ? 3 : 1);
        SideColumn.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(0);
        HotkeyHintLabel.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
    }
}
