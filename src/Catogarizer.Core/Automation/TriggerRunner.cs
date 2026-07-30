using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Automation;

/// <summary>
/// Thin dispatcher over ICategoryActionService - reuses its launch/find-window/position and
/// close-gracefully-then-kill logic rather than reimplementing process handling. Actions that
/// point at a since-deleted category/app id are silently skipped, same "never throw out of an
/// action" philosophy as CategoryActionService itself.
/// </summary>
public sealed class TriggerRunner
{
    private readonly ICategoryActionService _categoryActions;

    public TriggerRunner(ICategoryActionService categoryActions) => _categoryActions = categoryActions;

    public IReadOnlyList<AppActionResult> Run(Trigger trigger, LibraryService library)
    {
        var results = new List<AppActionResult>();
        foreach (var action in trigger.Actions)
        {
            switch (action.Type)
            {
                case TriggerActionType.OpenCategory:
                    RunOpenCategory(action, library, results);
                    break;
                case TriggerActionType.OpenApp:
                    RunOpenApp(action, library, results);
                    break;
                case TriggerActionType.CloseApps:
                    RunCloseApps(action, library, results);
                    break;
            }
        }
        return results;
    }

    private void RunOpenCategory(TriggerAction action, LibraryService library, List<AppActionResult> results)
    {
        var category = library.Categories.FirstOrDefault(c => c.Id == action.CategoryId);
        if (category is null) return;

        var apps = category.AppIds
            .Select(id => library.Apps.FirstOrDefault(a => a.Id == id))
            .OfType<AppEntry>()
            .ToList();
        results.AddRange(_categoryActions.Open(apps).AppResults);
    }

    private void RunOpenApp(TriggerAction action, LibraryService library, List<AppActionResult> results)
    {
        var app = library.Apps.FirstOrDefault(a => a.Id == action.AppId);
        if (app is not null)
            results.Add(_categoryActions.OpenApp(app));
    }

    private void RunCloseApps(TriggerAction action, LibraryService library, List<AppActionResult> results)
    {
        var apps = action.AppIdsToClose
            .Select(id => library.Apps.FirstOrDefault(a => a.Id == id))
            .OfType<AppEntry>();
        foreach (var app in apps)
            results.Add(_categoryActions.CloseApp(app));
    }
}
