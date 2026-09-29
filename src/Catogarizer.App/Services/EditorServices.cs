using Catogarizer.Core.Automation;
using Catogarizer.Core.Services;

namespace Catogarizer.App.Services;

/// <summary>What the home and the category editor need to edit the library and act on apps.</summary>
public sealed record EditorServices(
    LibraryService Library,
    IDialogService Dialogs,
    IInstalledAppFinder InstalledAppFinder,
    IProcessLauncher ProcessLauncher,
    IWindowFinder WindowFinder,
    IWindowManager WindowManager,
    IMonitorService MonitorService,
    ICategoryActionService CategoryActions,
    IAutostartService Autostart,
    TriggerRunner TriggerRunner);
