using Catogarizer.Core.Automation;
using Catogarizer.Core.Services;
using Catogarizer.Core.Tests.Fakes;

namespace Catogarizer.Core.Tests;

public sealed class TriggerRunnerTests
{
    private readonly FakeConfigStore _store = new();
    private readonly FakeCategoryActionService _categoryActions = new();
    private readonly LibraryService _library;
    private readonly TriggerRunner _runner;

    public TriggerRunnerTests()
    {
        _library = new LibraryService(_store, fileExists: _ => true);
        _runner = new TriggerRunner(_categoryActions);
    }

    [Fact]
    public void Run_OpenCategoryAction_OpensAllAppsInTheCategory()
    {
        var app1 = _library.AddApp("VS Code", @"C:\code.exe");
        var app2 = _library.AddApp("Slack", @"C:\slack.exe");
        var category = _library.AddCategory("Work");
        _library.AddAppToCategory(category.Id, app1.Id);
        _library.AddAppToCategory(category.Id, app2.Id);
        var trigger = MakeTrigger(new TriggerAction { Type = TriggerActionType.OpenCategory, CategoryId = category.Id });

        var results = _runner.Run(trigger, _library);

        Assert.Equal(2, results.Count);
        Assert.Contains("Open:VS Code,Slack", _categoryActions.Calls);
    }

    [Fact]
    public void Run_OpenAppAction_OpensJustThatApp()
    {
        var app = _library.AddApp("VS Code", @"C:\code.exe");
        var trigger = MakeTrigger(new TriggerAction { Type = TriggerActionType.OpenApp, AppId = app.Id });

        var results = _runner.Run(trigger, _library);

        Assert.Single(results);
        Assert.Contains("OpenApp:VS Code", _categoryActions.Calls);
    }

    [Fact]
    public void Run_CloseAppsAction_ClosesEachListedApp()
    {
        var app1 = _library.AddApp("VS Code", @"C:\code.exe");
        var app2 = _library.AddApp("Slack", @"C:\slack.exe");
        var trigger = MakeTrigger(new TriggerAction { Type = TriggerActionType.CloseApps, AppIdsToClose = [app1.Id, app2.Id] });

        var results = _runner.Run(trigger, _library);

        Assert.Equal(2, results.Count);
        Assert.Contains("CloseApp:VS Code", _categoryActions.Calls);
        Assert.Contains("CloseApp:Slack", _categoryActions.Calls);
    }

    [Fact]
    public void Run_ActionsRunInOrder()
    {
        var app = _library.AddApp("VS Code", @"C:\code.exe");
        var trigger = MakeTrigger(
            new TriggerAction { Type = TriggerActionType.OpenApp, AppId = app.Id },
            new TriggerAction { Type = TriggerActionType.CloseApps, AppIdsToClose = [app.Id] });

        _runner.Run(trigger, _library);

        Assert.Equal(["OpenApp:VS Code", "CloseApp:VS Code"], _categoryActions.Calls);
    }

    [Fact]
    public void Run_ActionReferencingDeletedCategory_IsSkippedWithoutThrowing()
    {
        var trigger = MakeTrigger(new TriggerAction { Type = TriggerActionType.OpenCategory, CategoryId = Guid.NewGuid() });

        var results = _runner.Run(trigger, _library);

        Assert.Empty(results);
    }

    [Fact]
    public void Run_ActionReferencingDeletedApp_IsSkippedWithoutThrowing()
    {
        var trigger = MakeTrigger(new TriggerAction { Type = TriggerActionType.OpenApp, AppId = Guid.NewGuid() });

        var results = _runner.Run(trigger, _library);

        Assert.Empty(results);
    }

    private static Trigger MakeTrigger(params TriggerAction[] actions) =>
        new() { Name = "Test", Type = TriggerType.Manual, Actions = actions.ToList() };
}
