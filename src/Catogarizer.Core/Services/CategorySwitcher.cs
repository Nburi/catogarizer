using Catogarizer.Core.Models;

namespace Catogarizer.Core.Services;

/// <summary>
/// The one switching path for every surface (home, palette, tray, CLI, triggers, hotkey
/// double-tap): resolves a category's template and blocklist from the library and keeps
/// blocking in step with the active category.
/// </summary>
public sealed class CategorySwitcher
{
    private readonly LibraryService _library;
    private readonly ICategorySwitchService _switch;
    private readonly IAppBlockingService _blocking;
    private readonly object _lock = new();

    public CategorySwitcher(LibraryService library, ICategorySwitchService switchService, IAppBlockingService blocking)
    {
        _library = library;
        _switch = switchService;
        _blocking = blocking;
    }

    public Guid ActiveCategoryId => _switch.ActiveCategoryId;
    public Guid? PreviousCategoryId => _switch.PreviousCategoryId;
    public DateTime ActiveSince => _switch.ActiveSince;
    public bool IsOpeningApps => _switch.IsOpeningApps;

    /// <inheritdoc cref="ICategorySwitchService.StateChanged"/>
    public event Action? StateChanged
    {
        add => _switch.StateChanged += value;
        remove => _switch.StateChanged -= value;
    }

    public IReadOnlyDictionary<Guid, IReadOnlyList<OpenWindowInfo>> GetSessions() => _switch.GetSessions();

    /// <summary>Null if the category no longer exists.</summary>
    public SwitchResult? SwitchTo(Guid categoryId)
    {
        lock (_lock)
        {
            var category = _library.Categories.FirstOrDefault(c => c.Id == categoryId);
            if (category is null && categoryId != CategorySwitchService.Uncategorized) return null;

            var outgoing = _switch.ActiveCategoryId;
            if (outgoing != categoryId)
            {
                // Lift the outgoing blocklist before the new template launches: Deep Work blocking
                // Slack must not kill the Slack that Comms is about to open.
                _blocking.DeactivateCategory(outgoing);
                if (category is not null)
                    _blocking.ActivateCategory(category.Id, _library.BlockedAppsOf(category));
            }

            IReadOnlyList<AppEntry> template = category is null ? [] : _library.AppsOf(category);
            return _switch.SwitchTo(categoryId, template);
        }
    }

    public SwitchResult? SwitchBack() => _switch.PreviousCategoryId is { } previous ? SwitchTo(previous) : null;

    /// <summary>Case-insensitive; "Unsorted" switches to the implicit category. Null if nothing matches.</summary>
    public SwitchResult? SwitchByName(string name)
    {
        var trimmed = name.Trim();
        if (string.Equals(trimmed, CategorySwitchService.UncategorizedName, StringComparison.OrdinalIgnoreCase))
            return SwitchTo(CategorySwitchService.Uncategorized);

        var category = _library.Categories.FirstOrDefault(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        return category is null ? null : SwitchTo(category.Id);
    }

    public void DeleteCategory(Guid categoryId)
    {
        lock (_lock)
        {
            _library.DeleteCategory(categoryId);
            _blocking.DeactivateCategory(categoryId);
            _switch.ReleaseCategory(categoryId);
        }
    }

    public void ApplyPinnedApps() => _switch.ApplyPinnedApps();

    /// <summary>Re-applies the active category's blocklist after it was edited.</summary>
    public void RefreshBlocking()
    {
        lock (_lock)
        {
            var active = _library.Categories.FirstOrDefault(c => c.Id == _switch.ActiveCategoryId);
            if (active is not null)
                _blocking.ActivateCategory(active.Id, _library.BlockedAppsOf(active));
        }
    }

    public void ShowAllAndReset()
    {
        lock (_lock)
        {
            _blocking.DeactivateCategory(_switch.ActiveCategoryId);
            _switch.ShowAllAndReset();
        }
    }
}
