namespace Catogarizer.Core.Services;

public static class AppEntryValidator
{
    public static ValidationResult ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name) ? ValidationResult.Fail("App name can't be empty.") : ValidationResult.Ok();

    /// <param name="fileExists">Injected so this stays testable without touching the real filesystem.</param>
    public static ValidationResult ValidateExecutablePath(string path, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Fail("Choose an application.");

        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Fail("Choose an .exe file.");

        if (!fileExists(path))
            return ValidationResult.Fail("That file doesn't exist.");

        return ValidationResult.Ok();
    }
}
