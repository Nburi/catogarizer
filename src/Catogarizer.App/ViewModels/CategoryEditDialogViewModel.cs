using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class CategoryEditDialogViewModel : ObservableObject
{
    private readonly IReadOnlyList<Category> _existingCategories;
    private readonly Guid? _editingCategoryId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public string HeadingText => _editingCategoryId is null ? "New category" : "Rename category";
    public string SaveButtonText => _editingCategoryId is null ? "Add" : "Save";

    public event EventHandler? RequestClose;

    public CategoryEditDialogViewModel(IReadOnlyList<Category> existingCategories, Category? editing = null)
    {
        _existingCategories = existingCategories;
        _editingCategoryId = editing?.Id;
        if (editing is not null) _name = editing.Name;
    }

    partial void OnNameChanged(string value)
    {
        var result = CategoryValidator.ValidateName(value, _existingCategories, _editingCategoryId);
        ErrorMessage = value.Length == 0 ? null : (result.IsValid ? null : result.ErrorMessage);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => RequestClose?.Invoke(this, EventArgs.Empty);

    private bool CanSave() => CategoryValidator.ValidateName(Name, _existingCategories, _editingCategoryId).IsValid;
}
