using System.Text.Json;
using Catogarizer.Core.Models;

namespace Catogarizer.Core.Persistence;

public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonConfigStore(string path)
    {
        _path = path;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_path)) return new AppConfig();

        string json;
        try
        {
            json = File.ReadAllText(_path);
        }
        catch (IOException ex)
        {
            throw new ConfigCorruptException($"The configuration file at \"{_path}\" could not be read.", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            throw new ConfigCorruptException($"The configuration file at \"{_path}\" is corrupted and could not be parsed.", ex);
        }
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, Options);
        var tmpPath = _path + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _path, overwrite: true);
    }
}
