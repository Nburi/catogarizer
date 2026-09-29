using System.Linq;
using System.Windows;
using Catogarizer.App.ViewModels;
using Catogarizer.App.Views;
using Catogarizer.Core.Models;

namespace Catogarizer.App.Services;

public sealed class DialogService : IDialogService
{
    /// <summary>Dialogs open over whatever the user is looking at (e.g. the category editor), not always the main window.</summary>
    private static Window? Owner =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
        ?? (Application.Current.MainWindow is { IsVisible: true } main ? main : null);

    private static bool? ShowModal(Window window)
    {
        window.Owner = Owner;
        if (window.Owner is null) window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return window.ShowDialog();
    }

    public (string Name, string ExecutablePath, string? Arguments)? ShowAppEdit(AppEditDialogViewModel viewModel) =>
        ShowModal(new AppEditWindow(viewModel)) == true ? viewModel.Result : null;

    public string? ShowCategoryEdit(CategoryEditDialogViewModel viewModel) =>
        ShowModal(new CategoryEditWindow(viewModel)) == true ? viewModel.Name.Trim() : null;

    public void ShowCategoryEditor(CategoryEditorViewModel viewModel) => ShowModal(new CategoryEditorWindow(viewModel));

    public bool ShowConfirm(string heading, string message, string confirmText = "Delete") =>
        ShowModal(new ConfirmWindow(heading, message, confirmText)) == true;

    public (bool Saved, WindowRect? Placement) ShowPlacement(PlacementDialogViewModel viewModel)
    {
        var ok = ShowModal(new PlacementWindow(viewModel));
        return (ok == true, viewModel.Result);
    }

    public bool ShowSettings(SettingsViewModel viewModel) => ShowModal(new SettingsWindow(viewModel)) == true;

    public void ShowTriggers(TriggersViewModel viewModel) => ShowModal(new TriggersWindow(viewModel));

    public bool ShowTriggerEdit(TriggerEditDialogViewModel viewModel) => ShowModal(new TriggerEditWindow(viewModel)) == true;
}
