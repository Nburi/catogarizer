namespace Catogarizer.Core.Automation;

/// <summary>
/// One step in a trigger's action list. Which fields apply depends on <see cref="Type"/>:
/// CategoryId for OpenCategory, AppId for OpenApp ("open at a specific location" is just
/// opening an AppEntry that already has a captured Placement), AppIdsToClose for CloseApps.
/// </summary>
public sealed class TriggerAction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public TriggerActionType Type { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? AppId { get; set; }
    public List<Guid> AppIdsToClose { get; set; } = new();
}
