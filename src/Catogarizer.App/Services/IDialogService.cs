using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Services;

public interface IDialogService
{
    /// <returns>The dialog's result if saved, or null if cancelled.</returns>
    (string Name, string ExecutablePath, string? Arguments)? ShowAppEdit(AppEditDialogViewModel viewModel);

    string? ShowCategoryEdit(CategoryEditDialogViewModel viewModel);

    bool ShowConfirm(string heading, string message, string confirmText = "Delete");
}
