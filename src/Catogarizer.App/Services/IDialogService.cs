using Catogarizer.App.ViewModels;
using Catogarizer.Core.Models;

namespace Catogarizer.App.Services;

public interface IDialogService
{
    /// <returns>The dialog's result if saved, or null if cancelled.</returns>
    (string Name, string ExecutablePath, string? Arguments)? ShowAppEdit(AppEditDialogViewModel viewModel);

    string? ShowCategoryEdit(CategoryEditDialogViewModel viewModel);

    bool ShowConfirm(string heading, string message, string confirmText = "Delete");

    /// <returns>(true, placement) if saved (placement may itself be null - "cleared"), (false, _) if cancelled.</returns>
    (bool Saved, WindowRect? Placement) ShowPlacement(PlacementDialogViewModel viewModel);

    /// <returns>true if saved, false if cancelled.</returns>
    bool ShowSettings(SettingsViewModel viewModel);
}
