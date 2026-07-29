using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using H.NotifyIcon;

namespace Catogarizer.App.Services;

/// <summary>
/// Owns the tray icon, its context menu (categories for quick switching, Start
/// with Windows toggle, Exit), and minimize/close-to-tray behavior for the
/// main window - closing or minimizing the window hides it instead of
/// exiting the app; only the tray's own Exit item really shuts down.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly LibraryService _library;
    private readonly ICategoryActionService _categoryActionService;
    private readonly IAppBlockingService _appBlockingService;
    private readonly IAutostartService _autostartService;
    private readonly Window _mainWindow;
    private readonly TaskbarIcon _icon;
    private bool _isExiting;

    public TrayIconController(LibraryService library, ICategoryActionService categoryActionService,
        IAppBlockingService appBlockingService, IAutostartService autostartService, Window mainWindow)
    {
        _library = library;
        _categoryActionService = categoryActionService;
        _appBlockingService = appBlockingService;
        _autostartService = autostartService;
        _mainWindow = mainWindow;

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ToolTipText = "Catogarizer",
        };
        _icon.TrayLeftMouseUp += (_, _) => ShowMainWindow();
        _icon.TrayContextMenuOpen += (_, _) => _icon.ContextMenu = BuildMenu();

        // A system toast, not a themed in-app dialog, is the right call here: the user is
        // very likely in a *different* app when a blocked one gets closed out from under
        // them, so a notification tied to Catogarizer's own (possibly hidden) window
        // wouldn't even be seen.
        // AppBlocked fires from the process watcher's background polling thread, not the
        // UI thread - ShowNotification needs to run on the dispatcher.
        _appBlockingService.AppBlocked += appName => _mainWindow.Dispatcher.Invoke(() => _icon.ShowNotification(
            "App blocked",
            $"\"{appName}\" was closed - blocked while this category is active.",
            H.NotifyIcon.Core.NotificationIcon.Warning));

        _mainWindow.Closing += MainWindow_Closing;
        _mainWindow.StateChanged += MainWindow_StateChanged;
    }

    public void ShowMainWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

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
        var categories = _library.Categories.OrderBy(c => c.SortOrder).ToList();

        foreach (var category in categories)
        {
            var item = new MenuItem { Header = category.Name };
            item.Click += (_, _) =>
            {
                var apps = ResolveApps(category);
                _appBlockingService.ActivateCategory(category.Id, ResolveBlockedApps(category));
                _ = Task.Run(() => _categoryActionService.Open(apps));
            };
            menu.Items.Add(item);
        }

        if (categories.Count > 0)
            menu.Items.Add(new Separator());

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

    private List<AppEntry> ResolveApps(Category category) =>
        category.AppIds
            .Select(id => _library.Apps.FirstOrDefault(a => a.Id == id))
            .Where(a => a is not null)
            .Cast<AppEntry>()
            .ToList();

    private List<BlockedApp> ResolveBlockedApps(Category category) =>
        category.BlockedAppIds
            .Select(id => _library.BlockedApps.FirstOrDefault(b => b.Id == id))
            .Where(b => b is not null)
            .Cast<BlockedApp>()
            .ToList();

    public void Dispose() => _icon.Dispose();
}
