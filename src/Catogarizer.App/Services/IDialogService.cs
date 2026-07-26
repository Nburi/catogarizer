using Catogarizer.Core.Models;

namespace Catogarizer.App.Services;

public interface IDialogService
{
    /// <summary>Shows the add/edit category dialog. Returns the (trimmed) name, or null if canceled.</summary>
    string? ShowCategoryEdit(Category? existing, IReadOnlyList<Category> allCategories);

    /// <summary>Shows the add/edit app dialog. Returns the built entry, or null if canceled.</summary>
    AppEntry? ShowAppEdit(AppEntry? existing, int order);

    bool ShowConfirm(string title, string message, string confirmText = "Delete");
}
