using System.Text.Json;
using Catogarizer.Core.Models;

namespace Catogarizer.Core.Persistence;

public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _configPath;

    public JsonConfigStore(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Catogarizer",
            "config.json");
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
            return new AppConfig();

        await using var stream = File.OpenRead(_configPath);
        try
        {
            var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, SerializerOptions, cancellationToken);
            return config ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            throw new ConfigCorruptException(_configPath, ex);
        }
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_configPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(_configPath)}.tmp-{Guid.NewGuid():N}");
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, _configPath, overwrite: true);
    }
}
