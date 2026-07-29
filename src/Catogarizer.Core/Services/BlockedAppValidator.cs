namespace Catogarizer.Core.Services;

public static class BlockedAppValidator
{
    public static ValidationResult ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name) ? ValidationResult.Fail("Name can't be empty.") : ValidationResult.Ok();

    /// <summary>
    /// No file-exists check (unlike AppEntryValidator) - a blocked app is
    /// identified by process name or path and doesn't need to be launchable
    /// from this machine right now to be worth blocking.
    /// </summary>
    public static ValidationResult ValidateProcessNameOrPath(string value) =>
        string.IsNullOrWhiteSpace(value) ? ValidationResult.Fail("Choose an application to block.") : ValidationResult.Ok();
}
