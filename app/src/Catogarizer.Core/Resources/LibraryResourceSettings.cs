using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.Core.Resources;

/// <summary>The user's decisions, read live from the library so a change applies on the next look.</summary>
public sealed class LibraryResourceSettings : IResourceSettings
{
    private readonly LibraryService _library;

    public LibraryResourceSettings(LibraryService library)
    {
        _library = library;
    }

    public bool Enabled => _library.Settings.SaveResources;

    public bool IsPinned(string processName) =>
        _library.PinnedApps.Any(p => string.Equals(ProcessPattern.ProcessName(p.ProcessNameOrPath), processName, StringComparison.OrdinalIgnoreCase));

    public bool IsAppFreezable(string processName) => _library.FindFreezableApp(processName) is not null;

    public FreezeMode FreezeModeOf(Guid categoryId) => _library.FreezeModeOf(categoryId);
}
