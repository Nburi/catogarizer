namespace Catogarizer.Core.Persistence;

/// <summary>
/// Thrown when the config file exists but can't be parsed, so the caller
/// can show a specific, readable message instead of an unhandled crash.
/// </summary>
public sealed class ConfigCorruptException : Exception
{
    public ConfigCorruptException(string message, Exception inner) : base(message, inner)
    {
    }
}
