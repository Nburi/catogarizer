using System.Text.Json;

namespace Catogarizer.Core.Persistence;

public sealed class JsonHiddenWindowStore : IHiddenWindowStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonHiddenWindowStore(string path)
    {
        _path = path;
    }

    public IReadOnlyList<HiddenWindowRecord> Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_path)) return [];
                return JsonSerializer.Deserialize<List<HiddenWindowRecord>>(File.ReadAllText(_path)) ?? [];
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Unreadable ledger: recovery can't help anyway, and blocking startup over it
                // would be worse than the windows it can no longer name.
                return [];
            }
        }
    }

    public void Save(IReadOnlyList<HiddenWindowRecord> records)
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmpPath = _path + ".tmp";
            File.WriteAllText(tmpPath, JsonSerializer.Serialize(records));
            File.Move(tmpPath, _path, overwrite: true);
        }
    }
}
