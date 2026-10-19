using System.Text.Json;

namespace Catogarizer.Core.Persistence;

public sealed class JsonThrottleLedger : IThrottleLedger
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonThrottleLedger(string path)
    {
        _path = path;
    }

    public IReadOnlyList<ThrottleRecord> Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_path)) return [];
                return JsonSerializer.Deserialize<List<ThrottleRecord>>(File.ReadAllText(_path)) ?? [];
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Unreadable ledger: recovery can't name the processes anyway, and blocking startup over it is worse.
                return [];
            }
        }
    }

    public void Save(IReadOnlyList<ThrottleRecord> records)
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
