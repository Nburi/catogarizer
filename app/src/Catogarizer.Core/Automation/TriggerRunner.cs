using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Automation;

/// <summary>
/// Thin dispatcher: category actions go through the switcher (a trigger "opening" a category
/// switches to it, same as the user would), app actions through ICategoryActionService. Actions
/// that point at a since-deleted category/app id are silently skipped, same "never throw out of
/// an action" philosophy as CategoryActionService itself.
/// </summary>
public sealed class TriggerRunner
{
    private readonly ICategoryActionService _categoryActions;
    private readonly Func<Guid, SwitchResult?> _switchToCategory;

    public TriggerRunner(ICategoryActionService categoryActions, Func<Guid, SwitchResult?> switchToCategory)
    {
        _categoryActions = categoryActions;
        _switchToCategory = switchToCategory;
    }

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
        if (action.CategoryId is not { } categoryId || library.Categories.All(c => c.Id != categoryId)) return;
        if (_switchToCategory(categoryId) is { } switched)
            results.AddRange(switched.Failures);
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
