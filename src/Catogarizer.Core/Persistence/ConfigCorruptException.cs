namespace Catogarizer.Core.Persistence;

/// <summary>
/// Thrown when the config file exists but can't be parsed, so the caller can
/// surface a real error instead of silently discarding the user's saved data.
/// </summary>
public sealed class ConfigCorruptException : Exception
{
    public string ConfigPath { get; }

    public ConfigCorruptException(string configPath, Exception innerException)
        : base($"Config file at '{configPath}' could not be parsed.", innerException)
    {
        ConfigPath = configPath;
    }
}
