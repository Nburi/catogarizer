using Catogarizer.Core;
using Catogarizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public partial class CategoryEditDialogViewModel : ObservableObject
{
    private readonly IReadOnlyList<Category> _allCategories;
    private readonly Guid? _editingId;

    public string Title { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? nameError;

    public bool HasError => !string.IsNullOrEmpty(NameError);

    public CategoryEditDialogViewModel(Category? existing, IReadOnlyList<Category> allCategories)
    {
        _allCategories = allCategories;
        _editingId = existing?.Id;
        name = existing?.Name ?? string.Empty;
        Title = existing is null ? "New Category" : "Edit Category";
    }

    public bool TryValidate(out string trimmedName)
    {
        var errors = Validation.ValidateCategoryName(Name, _allCategories, _editingId);
        if (errors.Count > 0)
        {
            NameError = errors[0];
            trimmedName = Name;
            return false;
        }

        NameError = null;
        trimmedName = Name.Trim();
        return true;
    }
}
