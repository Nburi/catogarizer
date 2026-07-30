using System.Globalization;
using Catogarizer.Core.Automation;

namespace Catogarizer.Core.Services;

public static class TriggerValidator
{
    private const int MaxNameLength = 60;

    public static ValidationResult ValidateName(string name, IEnumerable<Trigger> existing, Guid? excludingId = null)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
            return ValidationResult.Fail("Trigger name can't be empty.");

        if (trimmed.Length > MaxNameLength)
            return ValidationResult.Fail($"Trigger name is too long (max {MaxNameLength} characters).");

        var duplicate = existing.Any(t => t.Id != excludingId && string.Equals(t.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            return ValidationResult.Fail($"A trigger named \"{trimmed}\" already exists.");

        return ValidationResult.Ok();
    }

    /// <summary>Only meaningful when the trigger's Type is Time - other types ignore TimeOfDay.</summary>
    public static ValidationResult ValidateTimeOfDay(TriggerType type, string? timeOfDay)
    {
        if (type != TriggerType.Time)
            return ValidationResult.Ok();

        if (string.IsNullOrWhiteSpace(timeOfDay))
            return ValidationResult.Fail("Choose a time of day.");

        if (!DateTime.TryParseExact(timeOfDay, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return ValidationResult.Fail("Time must be in HH:mm format (e.g. 08:00).");

        return ValidationResult.Ok();
    }
}
