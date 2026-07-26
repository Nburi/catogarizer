namespace Catogarizer.Core.Automation;

/// <summary>
/// "When TriggerType fires, do Action to the category TargetCategoryId."
/// Persisted in AppConfig starting now so the schema doesn't need a migration
/// once a trigger is actually implemented, but nothing evaluates these rules
/// yet - there is no UI to create them and no scheduler running. A future
/// engine would call the same ICategoryActionService methods the UI already
/// uses (Open/Close/MinimizeAsync) for the Action, keeping one execution path
/// for both manual clicks and automated triggers.
/// </summary>
public sealed class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public AutomationTriggerType TriggerType { get; set; } = AutomationTriggerType.Scheduled;
    public string TriggerConfig { get; set; } = string.Empty;
    public AutomationActionType Action { get; set; } = AutomationActionType.OpenCategory;
    public Guid TargetCategoryId { get; set; }
}
