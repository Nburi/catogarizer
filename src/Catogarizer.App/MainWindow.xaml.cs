using System.Windows;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Automation;
using Catogarizer.Core.Services;

namespace Catogarizer.App;

public partial class MainWindow : Window
{
    public MainWindow(LibraryService library, IInstalledAppFinder installedAppFinder, IDialogService dialogService,
        IProcessLauncher processLauncher, IWindowFinder windowFinder, IWindowManager windowManager,
        IMonitorService monitorService, ICategoryActionService categoryActionService, CategorySwitcher switcher,
        IAutostartService autostartService, TriggerRunner triggerRunner, ThemeService themeService, Action<string> onHotkeyChanged)
    {
        InitializeComponent();
        DataContext = new MainViewModel(library, installedAppFinder, dialogService, processLauncher, windowFinder,
            windowManager, monitorService, categoryActionService, switcher, autostartService, triggerRunner, themeService, onHotkeyChanged);
    }
}
