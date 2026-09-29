using Catogarizer.Core.Automation;
using Catogarizer.Core.Models;
using Catogarizer.Core.Persistence;

namespace Catogarizer.Core.Services;

/// <summary>
/// Owns the in-memory <see cref="AppConfig"/> and every category/app mutation,
/// saving after each change. Assumes callers have already run the relevant
/// <see cref="CategoryValidator"/>/<see cref="AppEntryValidator"/> checks for
/// inline UI feedback - the checks re-run here too, as a safety net, and
/// throw <see cref="ArgumentException"/> if something invalid slips through.
/// </summary>
public sealed class LibraryService
{
    private readonly IConfigStore _configStore;
    private readonly Func<string, bool> _fileExists;
    private AppConfig _config;

    public LibraryService(IConfigStore configStore, Func<string, bool>? fileExists = null)
    {
        _configStore = configStore;
        _fileExists = fileExists ?? File.Exists;
        _config = configStore.Load();
        AssignMissingHues();
    }

    /// <summary>Configs from before category colors existed get a color per category once, in their order.</summary>
    private void AssignMissingHues()
    {
        var missing = _config.Categories.Where(c => c.Hue is null).OrderBy(c => c.SortOrder).ToList();
        if (missing.Count == 0) return;
        foreach (var category in missing)
            category.Hue = Theming.CategoryHues.Next(_config.Categories.Where(c => c.Hue is not null).Select(c => c.Hue!.Value));
        Save();
    }

    public IReadOnlyList<Category> Categories => _config.Categories;
    public IReadOnlyList<AppEntry> Apps => _config.Apps;
    public IReadOnlyList<BlockedApp> BlockedApps => _config.BlockedApps;
    /// <summary>Read from the window watcher's thread; mutations replace the list instead of changing it.</summary>
    public IReadOnlyList<PinnedApp> PinnedApps => _config.PinnedApps;
    public IReadOnlyList<Trigger> Triggers => _config.Triggers;

    public IReadOnlyList<AppEntry> AppsOf(Category category) =>
        category.AppIds.Select(id => _config.Apps.FirstOrDefault(a => a.Id == id)).OfType<AppEntry>().ToList();

    public IReadOnlyList<BlockedApp> BlockedAppsOf(Category category) =>
        category.BlockedAppIds.Select(id => _config.BlockedApps.FirstOrDefault(b => b.Id == id)).OfType<BlockedApp>().ToList();
    public AppSettings Settings => _config.Settings;

    /// <summary>The category's name; the implicit Unsorted category (and a deleted one) reads "Unsorted".</summary>
    public string NameOf(Guid categoryId) =>
        _config.Categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? CategorySwitchService.UncategorizedName;

    /// <summary>Number-key shortcut: 1-9 are the first nine categories in shelf order, 0 is Unsorted.</summary>
    public string? KeyOf(Guid categoryId)
    {
        if (categoryId == CategorySwitchService.Uncategorized) return "0";
        var index = OrderedCategories().FindIndex(c => c.Id == categoryId);
        return index is >= 0 and < 9 ? (index + 1).ToString() : null;
    }

    /// <summary>The reverse of <see cref="KeyOf"/>; null if no category has that number.</summary>
    public Guid? CategoryIdForKey(int number) => number switch
    {
        0 => CategorySwitchService.Uncategorized,
        >= 1 and <= 9 => OrderedCategories().ElementAtOrDefault(number - 1)?.Id,
        _ => null,
    };

    /// <summary>How many of the category's apps no longer exist at their path.</summary>
    public int MissingAppCount(Category category) => AppsOf(category).Count(a => !_fileExists(a.ExecutablePath));

    private List<Category> OrderedCategories() => _config.Categories.OrderBy(c => c.SortOrder).ToList();

    public void Reload() => _config = _configStore.Load();

    /// <summary>
    /// Autostart isn't included here - the Registry Run key (via IAutostartService) is
    /// the actual source of truth for that, not a persisted setting that could drift out
    /// of sync with it (e.g. if the user removes it from Windows' own Startup Apps page).
    /// </summary>
    public void UpdateSettings(bool startMinimized, string commandPaletteHotkey)
    {
        _config.Settings.StartMinimized = startMinimized;
        _config.Settings.CommandPaletteHotkey = commandPaletteHotkey;
        Save();
    }

    public void SetTheme(string themeId)
    {
        if (!Theming.ThemeCatalog.Exists(themeId))
            throw new ArgumentException($"There is no theme called \"{themeId}\".", nameof(themeId));
        _config.Settings.Theme = Theming.ThemeCatalog.Get(themeId).Id;
        Save();
    }

    // ---------------- Categories ----------------

    public Category AddCategory(string name)
    {
        var validation = CategoryValidator.ValidateName(name, _config.Categories);
        if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage, nameof(name));

        var category = new Category
        {
            Name = name.Trim(),
            SortOrder = _config.Categories.Count,
            Hue = Theming.CategoryHues.Next(_config.Categories.Select(c => c.Hue).OfType<double>()),
        };
        _config.Categories.Add(category);
        Save();
        return category;
    }

    public void RenameCategory(Guid categoryId, string newName)
    {
        var category = GetCategoryOrThrow(categoryId);
        var validation = CategoryValidator.ValidateName(newName, _config.Categories, excludingId: categoryId);
        if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage, nameof(newName));

        category.Name = newName.Trim();
        Save();
    }

    public void SetCategoryHue(Guid categoryId, double hue)
    {
        var category = GetCategoryOrThrow(categoryId);
        category.Hue = ((hue % 360) + 360) % 360;
        Save();
    }

    public void DeleteCategory(Guid categoryId)
    {
        var category = GetCategoryOrThrow(categoryId);
        _config.Categories.Remove(category);
        Save();
    }

    public void ReorderCategories(IReadOnlyList<Guid> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var category = _config.Categories.FirstOrDefault(c => c.Id == orderedIds[i]);
            if (category is not null) category.SortOrder = i;
        }
        Save();
    }

    // ---------------- Apps ----------------

    public AppEntry AddApp(string name, string executablePath, string? arguments = null)
    {
        var nameValidation = AppEntryValidator.ValidateName(name);
        if (!nameValidation.IsValid) throw new ArgumentException(nameValidation.ErrorMessage, nameof(name));
        var pathValidation = AppEntryValidator.ValidateExecutablePath(executablePath, _fileExists);
        if (!pathValidation.IsValid) throw new ArgumentException(pathValidation.ErrorMessage, nameof(executablePath));

        var app = new AppEntry { Name = name.Trim(), ExecutablePath = executablePath, Arguments = arguments };
        _config.Apps.Add(app);
        Save();
        return app;
    }

    /// <summary>Returns an existing app entry for this path if one exists, otherwise adds a new one.</summary>
    public AppEntry AddOrReuseApp(string name, string executablePath, string? arguments = null)
    {
        var existing = _config.Apps.FirstOrDefault(a => string.Equals(a.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase));
        return existing ?? AddApp(name, executablePath, arguments);
    }

    public void UpdateApp(Guid appId, string name, string executablePath, string? arguments)
    {
        var app = GetAppOrThrow(appId);
        var nameValidation = AppEntryValidator.ValidateName(name);
        if (!nameValidation.IsValid) throw new ArgumentException(nameValidation.ErrorMessage, nameof(name));
        var pathValidation = AppEntryValidator.ValidateExecutablePath(executablePath, _fileExists);
        if (!pathValidation.IsValid) throw new ArgumentException(pathValidation.ErrorMessage, nameof(executablePath));

        app.Name = name.Trim();
        app.ExecutablePath = executablePath;
        app.Arguments = arguments;
        Save();
    }

    public void DeleteApp(Guid appId)
    {
        var app = GetAppOrThrow(appId);
        foreach (var category in _config.Categories)
            category.AppIds.Remove(appId);
        _config.Apps.Remove(app);
        Save();
    }

    /// <summary>Null clears a captured placement - the app then opens at its own default position.</summary>
    public void SetAppPlacement(Guid appId, WindowRect? placement)
    {
        var app = GetAppOrThrow(appId);
        app.Placement = placement;
        Save();
    }

    // ---------------- Blocked apps ----------------

    public BlockedApp AddBlockedApp(string name, string processNameOrPath)
    {
        var nameValidation = BlockedAppValidator.ValidateName(name);
        if (!nameValidation.IsValid) throw new ArgumentException(nameValidation.ErrorMessage, nameof(name));
        var valueValidation = BlockedAppValidator.ValidateProcessNameOrPath(processNameOrPath);
        if (!valueValidation.IsValid) throw new ArgumentException(valueValidation.ErrorMessage, nameof(processNameOrPath));

        var blocked = new BlockedApp { Name = name.Trim(), ProcessNameOrPath = processNameOrPath };
        _config.BlockedApps.Add(blocked);
        Save();
        return blocked;
    }

    /// <summary>Returns an existing blocked-app entry for this value if one exists, otherwise adds a new one.</summary>
    public BlockedApp AddOrReuseBlockedApp(string name, string processNameOrPath)
    {
        var existing = _config.BlockedApps.FirstOrDefault(b => string.Equals(b.ProcessNameOrPath, processNameOrPath, StringComparison.OrdinalIgnoreCase));
        return existing ?? AddBlockedApp(name, processNameOrPath);
    }

    public void DeleteBlockedApp(Guid blockedAppId)
    {
        var blocked = GetBlockedAppOrThrow(blockedAppId);
        foreach (var category in _config.Categories)
            category.BlockedAppIds.Remove(blockedAppId);
        _config.BlockedApps.Remove(blocked);
        Save();
    }

    // ---------------- Pinned apps ----------------

    public PinnedApp AddPinnedApp(string name, string processNameOrPath)
    {
        var nameValidation = BlockedAppValidator.ValidateName(name);
        if (!nameValidation.IsValid) throw new ArgumentException(nameValidation.ErrorMessage, nameof(name));
        var valueValidation = BlockedAppValidator.ValidateProcessNameOrPath(processNameOrPath);
        if (!valueValidation.IsValid) throw new ArgumentException(valueValidation.ErrorMessage, nameof(processNameOrPath));

        var existing = _config.PinnedApps.FirstOrDefault(p => string.Equals(p.ProcessNameOrPath, processNameOrPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var pinned = new PinnedApp { Name = name.Trim(), ProcessNameOrPath = processNameOrPath };
        _config.PinnedApps = [.. _config.PinnedApps, pinned];
        Save();
        return pinned;
    }

    public void RemovePinnedApp(Guid pinnedAppId)
    {
        _config.PinnedApps = _config.PinnedApps.Where(p => p.Id != pinnedAppId).ToList();
        Save();
    }

    // ---------------- Category <-> App links ----------------

    public void AddAppToCategory(Guid categoryId, Guid appId)
    {
        var category = GetCategoryOrThrow(categoryId);
        GetAppOrThrow(appId);
        if (!category.AppIds.Contains(appId))
            category.AppIds.Add(appId);
        Save();
    }

    public void RemoveAppFromCategory(Guid categoryId, Guid appId)
    {
        var category = GetCategoryOrThrow(categoryId);
        category.AppIds.Remove(appId);
        Save();
    }

    // ---------------- Category <-> BlockedApp links ----------------

    public void AddBlockedAppToCategory(Guid categoryId, Guid blockedAppId)
    {
        var category = GetCategoryOrThrow(categoryId);
        GetBlockedAppOrThrow(blockedAppId);
        if (!category.BlockedAppIds.Contains(blockedAppId))
            category.BlockedAppIds.Add(blockedAppId);
        Save();
    }

    public void RemoveBlockedAppFromCategory(Guid categoryId, Guid blockedAppId)
    {
        var category = GetCategoryOrThrow(categoryId);
        category.BlockedAppIds.Remove(blockedAppId);
        Save();
    }

    // ---------------- Triggers ----------------

    public Trigger AddTrigger(string name, TriggerType type)
    {
        var validation = TriggerValidator.ValidateName(name, _config.Triggers);
        if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage, nameof(name));

        var trigger = new Trigger { Name = name.Trim(), Type = type };
        _config.Triggers.Add(trigger);
        Save();
        return trigger;
    }

    public void UpdateTrigger(Guid triggerId, string name, TriggerType type, string? timeOfDay, List<DayOfWeek> daysOfWeek, List<TriggerAction> actions)
    {
        var trigger = GetTriggerOrThrow(triggerId);
        var nameValidation = TriggerValidator.ValidateName(name, _config.Triggers, excludingId: triggerId);
        if (!nameValidation.IsValid) throw new ArgumentException(nameValidation.ErrorMessage, nameof(name));
        var timeValidation = TriggerValidator.ValidateTimeOfDay(type, timeOfDay);
        if (!timeValidation.IsValid) throw new ArgumentException(timeValidation.ErrorMessage, nameof(timeOfDay));

        trigger.Name = name.Trim();
        trigger.Type = type;
        trigger.TimeOfDay = type == TriggerType.Time ? timeOfDay : null;
        trigger.DaysOfWeek = type == TriggerType.Time ? daysOfWeek : new List<DayOfWeek>();
        trigger.Actions = actions;
        Save();
    }

    public void SetTriggerEnabled(Guid triggerId, bool enabled)
    {
        var trigger = GetTriggerOrThrow(triggerId);
        trigger.IsEnabled = enabled;
        Save();
    }

    public void DeleteTrigger(Guid triggerId)
    {
        var trigger = GetTriggerOrThrow(triggerId);
        _config.Triggers.Remove(trigger);
        Save();
    }

    private Trigger GetTriggerOrThrow(Guid triggerId) =>
        _config.Triggers.FirstOrDefault(t => t.Id == triggerId)
        ?? throw new InvalidOperationException($"No trigger with id {triggerId}.");

    private Category GetCategoryOrThrow(Guid categoryId) =>
        _config.Categories.FirstOrDefault(c => c.Id == categoryId)
        ?? throw new InvalidOperationException($"No category with id {categoryId}.");

    private AppEntry GetAppOrThrow(Guid appId) =>
        _config.Apps.FirstOrDefault(a => a.Id == appId)
        ?? throw new InvalidOperationException($"No app with id {appId}.");

    private BlockedApp GetBlockedAppOrThrow(Guid blockedAppId) =>
        _config.BlockedApps.FirstOrDefault(b => b.Id == blockedAppId)
        ?? throw new InvalidOperationException($"No blocked app with id {blockedAppId}.");

    private void Save() => _configStore.Save(_config);
}
