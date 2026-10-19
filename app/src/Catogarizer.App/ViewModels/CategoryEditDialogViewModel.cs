using Catogarizer.Core.Models;
using Catogarizer.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catogarizer.App.ViewModels;

public partial class CategoryEditDialogViewModel : ObservableObject
{
    private readonly IReadOnlyList<Category> _existingCategories;
    private readonly Guid? _editingCategoryId;
    private readonly string? _quickSuggestion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>A quick (temporary) category: the name is optional and an empty one is replaced by a generated one.</summary>
    public bool IsQuick => _quickSuggestion is not null;

    public string HeadingText => IsQuick ? "Quick category" : _editingCategoryId is null ? "New category" : "Rename category";
    public string SaveButtonText => IsQuick ? "Start" : _editingCategoryId is null ? "Add" : "Save";

    public string HintText => IsQuick
        ? $"Optional. Left empty it is called {_quickSuggestion}. It disappears when its last window closes, when you close it, or when Catogarizer restarts."
        : "For example Deep Work, Comms or Study. Next you pick the apps it opens.";

    public event EventHandler? RequestClose;

    /// <param name="quickSuggestion">Set for a quick category: the name it gets when the field stays empty.</param>
    public CategoryEditDialogViewModel(IReadOnlyList<Category> existingCategories, Category? editing = null, string? quickSuggestion = null)
    {
        _existingCategories = existingCategories;
        _editingCategoryId = editing?.Id;
        _quickSuggestion = quickSuggestion;
        if (editing is not null) _name = editing.Name;
    }

    partial void OnNameChanged(string value)
    {
        var result = CategoryValidator.ValidateName(value, _existingCategories, _editingCategoryId);
        ErrorMessage = value.Length == 0 ? null : (result.IsValid ? null : result.ErrorMessage);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => RequestClose?.Invoke(this, EventArgs.Empty);

    private bool CanSave() => (IsQuick && Name.Trim().Length == 0)
        || CategoryValidator.ValidateName(Name, _existingCategories, _editingCategoryId).IsValid;
}
