using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Catogarizer.Core.Services;
using H.NotifyIcon;

namespace Catogarizer.App.Services;

/// <summary>
/// Owns the tray icon, its context menu (switch between categories, show all hidden
/// windows, Start with Windows toggle, Exit), and minimize/close-to-tray behavior for the
/// main window - closing or minimizing the window hides it instead of exiting the app;
/// only the tray's own Exit item really shuts down.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly LibraryService _library;
    private readonly CategorySwitcher _switcher;
    private readonly IAutostartService _autostartService;
    private readonly Window _mainWindow;
    private readonly Action<SwitchResult> _onSwitched;
    private readonly TaskbarIcon _icon;
    private bool _isExiting;

    public TrayIconController(LibraryService library, CategorySwitcher switcher, IAppBlockingService appBlockingService,
        IAutostartService autostartService, Window mainWindow, Action<SwitchResult> onSwitched)
    {
        _library = library;
        _switcher = switcher;
        _autostartService = autostartService;
        _mainWindow = mainWindow;
        _onSwitched = onSwitched;

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ToolTipText = "Catogarizer",
        };
        _icon.TrayLeftMouseUp += (_, _) => ShowMainWindow();
        _icon.TrayContextMenuOpen += (_, _) => _icon.ContextMenu = BuildMenu();
        // Created in code (not XAML), so it must be created explicitly - otherwise every
        // notification throws "TrayIcon is not created". Efficiency mode off: it would
        // throttle the whole process, and switching has to stay instant.
        _icon.ForceCreate(enablesEfficiencyMode: false);

        // A system toast, not a themed in-app dialog, is the right call here: the user is
        // very likely in a *different* app when a blocked one gets closed out from under
        // them, so a notification tied to Catogarizer's own (possibly hidden) window
        // wouldn't even be seen.
        // AppBlocked fires from the process watcher's background polling thread, not the
        // UI thread - ShowNotification needs to run on the dispatcher.
        appBlockingService.AppBlocked += appName => _mainWindow.Dispatcher.BeginInvoke(() => Notify(
            "App held back",
            $"\"{appName}\" was closed. It's blocked while {ActiveCategoryName()} is active."));

        _switcher.StateChanged += () => _mainWindow.Dispatcher.BeginInvoke(UpdateToolTip);
        UpdateToolTip();

        _mainWindow.Closing += MainWindow_Closing;
        _mainWindow.StateChanged += MainWindow_StateChanged;
    }

    public void ShowMainWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>For problems the user should see even when no Catogarizer window is open.</summary>
    public void ShowProblem(string title, string message) => Notify(title, message);

    /// <summary>A notification is a courtesy: if Windows refuses it, the app carries on.</summary>
    private void Notify(string title, string message)
    {
        try
        {
            _icon.ShowNotification(title, message, H.NotifyIcon.Core.NotificationIcon.Warning);
        }
        catch (Exception)
        {
            // Nothing sensible to fall back to; the home window shows the same state.
        }
    }

    public void AllowExit() => _isExiting = true;

    private void UpdateToolTip() => _icon.ToolTipText = $"Catogarizer · {ActiveCategoryName()}";

    private string ActiveCategoryName() =>
        _library.Categories.FirstOrDefault(c => c.Id == _switcher.ActiveCategoryId)?.Name ?? CategorySwitchService.UncategorizedName;

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting) return;
        e.Cancel = true;
        _mainWindow.Hide();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.Hide();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var active = _switcher.ActiveCategoryId;

        if (_switcher.PreviousCategoryId is { } previous)
        {
            var name = _library.Categories.FirstOrDefault(c => c.Id == previous)?.Name ?? CategorySwitchService.UncategorizedName;
            var backItem = new MenuItem { Header = $"← Back to {name}", InputGestureText = "hotkey twice" };
            backItem.Click += async (_, _) =>
            {
                var result = await Task.Run(_switcher.SwitchBack);
                if (result is not null) _onSwitched(result);
            };
            menu.Items.Add(backItem);
            menu.Items.Add(new Separator());
        }

        foreach (var category in _library.Categories.OrderBy(c => c.SortOrder))
            menu.Items.Add(SwitchItem(category.Id, category.Name, category.Id == active));
        menu.Items.Add(SwitchItem(CategorySwitchService.Uncategorized, CategorySwitchService.UncategorizedName,
            active == CategorySwitchService.Uncategorized));

        menu.Items.Add(new Separator());

        var showAllItem = new MenuItem { Header = "Show all hidden windows" };
        showAllItem.Click += (_, _) => Task.Run(_switcher.ShowAllAndReset);
        menu.Items.Add(showAllItem);

        var showItem = new MenuItem { Header = "Show Catogarizer" };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        var autostartItem = new MenuItem { Header = "Start with Windows", IsCheckable = true, IsChecked = _autostartService.IsEnabled };
        autostartItem.Click += (_, _) =>
        {
            if (autostartItem.IsChecked) _autostartService.Enable();
            else _autostartService.Disable();
        };
        menu.Items.Add(autostartItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) =>
        {
            _isExiting = true;
            Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    private MenuItem SwitchItem(Guid categoryId, string name, bool isActive)
    {
        var item = new MenuItem { Header = name, IsCheckable = false, IsChecked = isActive };
        item.Click += async (_, _) =>
        {
            var result = await Task.Run(() => _switcher.SwitchTo(categoryId));
            if (result is not null) _onSwitched(result);
        };
        return item;
    }

    public void Dispose() => _icon.Dispose();
}
