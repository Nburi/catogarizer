using Catogarizer.Core.Models;

namespace Catogarizer.Core.Resources;

/// <summary>What the user decided, as the governor needs to ask it - from any thread.</summary>
public interface IResourceSettings
{
    /// <summary>The "Save resources while categories are parked" setting.</summary>
    bool Enabled { get; }

    bool IsPinned(string processName);

    /// <summary>The user allowed this app to be suspended while parked.</summary>
    bool IsAppFreezable(string processName);

    FreezeMode FreezeModeOf(Guid categoryId);

    /// <summary>Sound protects this category's apps while it is parked (the default). Off for games.</summary>
    bool KeepsSoundAwake(Guid categoryId);
}
