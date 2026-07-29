using Catogarizer.Core.Models;

namespace Catogarizer.Core.Persistence;

public interface IConfigStore
{
    /// <summary>Returns a fresh empty config if no file exists yet.</summary>
    /// <exception cref="ConfigCorruptException">The file exists but couldn't be parsed.</exception>
    AppConfig Load();

    void Save(AppConfig config);
}
