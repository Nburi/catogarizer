using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

public static class CategoryValidator
{
    private const int MaxNameLength = 60;

    public static ValidationResult ValidateName(string name, IEnumerable<Category> existing, Guid? excludingId = null)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
            return ValidationResult.Fail("Category name can't be empty.");

        if (trimmed.Length > MaxNameLength)
            return ValidationResult.Fail($"Category name is too long (max {MaxNameLength} characters).");

        var duplicate = existing.Any(c => c.Id != excludingId && string.Equals(c.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            return ValidationResult.Fail($"A category named \"{trimmed}\" already exists.");

        return ValidationResult.Ok();
    }
}
