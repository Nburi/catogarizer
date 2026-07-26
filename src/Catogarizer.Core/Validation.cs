using Catogarizer.Core.Models;

namespace Catogarizer.Core;

public static class Validation
{
    public static IReadOnlyList<string> ValidateAppEntry(AppEntry entry)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entry.Name))
            errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(entry.ExecutablePath))
            errors.Add("Executable path is required.");
        if (entry.Window.Width <= 0)
            errors.Add("Window width must be greater than 0.");
        if (entry.Window.Height <= 0)
            errors.Add("Window height must be greater than 0.");
        if (entry.LaunchDelayMs < 0)
            errors.Add("Launch delay cannot be negative.");

        return errors;
    }

    public static IReadOnlyList<string> ValidateCategoryName(
        string name,
        IEnumerable<Category> existing,
        Guid? editingId = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Category name is required.");
            return errors;
        }

        var duplicate = existing.Any(c =>
            c.Id != editingId && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            errors.Add($"A category named \"{name}\" already exists.");

        return errors;
    }
}
