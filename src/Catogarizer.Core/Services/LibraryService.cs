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
    }

    public IReadOnlyList<Category> Categories => _config.Categories;
    public IReadOnlyList<AppEntry> Apps => _config.Apps;

    public void Reload() => _config = _configStore.Load();

    // ---------------- Categories ----------------

    public Category AddCategory(string name)
    {
        var validation = CategoryValidator.ValidateName(name, _config.Categories);
        if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage, nameof(name));

        var category = new Category { Name = name.Trim(), SortOrder = _config.Categories.Count };
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

    private Category GetCategoryOrThrow(Guid categoryId) =>
        _config.Categories.FirstOrDefault(c => c.Id == categoryId)
        ?? throw new InvalidOperationException($"No category with id {categoryId}.");

    private AppEntry GetAppOrThrow(Guid appId) =>
        _config.Apps.FirstOrDefault(a => a.Id == appId)
        ?? throw new InvalidOperationException($"No app with id {appId}.");

    private void Save() => _configStore.Save(_config);
}
