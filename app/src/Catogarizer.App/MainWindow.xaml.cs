using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.App.Views;
using Catogarizer.Core.Services;

namespace Catogarizer.App;

public partial class MainWindow : Window
{
    private const double NarrowWidth = 860;

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _liveRefresh;

    public MainWindow(EditorServices services, CategorySwitcher switcher, ThemeService themeService,
        IAppBlockingService appBlockingService, UpdateNotifier updateNotifier, Action<string> onHotkeyChanged)
    {
        InitializeComponent();
        var build = Services.AppBuildInfo.Current;
        Title = build.WindowTitle;
        if (build.DevChipText is { } chip)
        {
            DevChipText.Text = chip;
            DevChip.Visibility = Visibility.Visible;
        }
        _viewModel = new MainViewModel(services, switcher, themeService, appBlockingService, updateNotifier, onHotkeyChanged);
        DataContext = _viewModel;
        _viewModel.NoticeRaised += (_, _) => PageScroll.ScrollToTop();

        // Window titles and "48 min" change without any event - refresh while visible only.
        _liveRefresh = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => _viewModel.Refresh(), Dispatcher);
        _liveRefresh.Stop();
        IsVisibleChanged += (_, _) =>
        {
            _viewModel.IsViewVisible = IsVisible;
            if (IsVisible) { _viewModel.Refresh(); _liveRefresh.Start(); }
            else _liveRefresh.Stop();
        };
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control && _viewModel.CanCloseActive)
        {
            e.Handled = true;
            await _viewModel.CloseActiveAsync();
            return;
        }

        if (Keyboard.FocusedElement is TextBox || Keyboard.Modifiers != ModifierKeys.None) return;
        if (KeyDigits.Of(e.Key) is not { } number) return;
        e.Handled = true;
        await _viewModel.SwitchByNumberAsync(number);
    }

    /// <summary>"+" starts a quick category; text input (not a key code) so it works on every keyboard layout.</summary>
    private async void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text != "+" || Keyboard.FocusedElement is TextBox) return;
        e.Handled = true;
        await _viewModel.NewQuickCategoryAsync();
    }

    /// <summary>Built per opening, so "Pin"/"Unpin" always matches the current state.</summary>
    private void WindowRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowRow row, ContextMenu: { } menu })
            menu.ItemsSource = _viewModel.BuildWindowMenu(row);
        else
            e.Handled = true;
    }

    /// <summary>Right-click menu of a shelf tile: close that category.</summary>
    private async void ShelfMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: ShelfItem item } } })
            await _viewModel.CloseShelfCategoryAsync(item);
    }

    private async void WindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: WindowMenuEntry entry } item) return;
        if (ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu { PlacementTarget: FrameworkElement { DataContext: WindowRow row } }) return;

        switch (entry)
        {
            case MoveTarget target:
                await _viewModel.MoveWindowAsync(row.Handle, target.Id);
                break;
            case PinMenuEntry:
                await _viewModel.TogglePinAsync(row);
                break;
            case FreezeMenuEntry:
                await _viewModel.ToggleFreezeAsync(row);
                break;
        }
    }

    public static readonly DependencyProperty ShelfColumnsProperty =
        DependencyProperty.Register(nameof(ShelfColumns), typeof(int), typeof(MainWindow), new PropertyMetadata(4));

    /// <summary>How many shelf tiles fit side by side; tiles stretch to fill the row.</summary>
    public int ShelfColumns
    {
        get => (int)GetValue(ShelfColumnsProperty);
        set => SetValue(ShelfColumnsProperty, value);
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
        HotkeyHintKeys.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;

        const double minTileWidth = 220, gap = 12, pagePadding = 56, scrollbar = 12;
        var available = ActualWidth - pagePadding - scrollbar;
        ShelfColumns = Math.Max(1, (int)((available + gap) / (minTileWidth + gap)));
    }
}
