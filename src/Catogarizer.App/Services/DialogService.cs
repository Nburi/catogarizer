using System.Windows;
using Catogarizer.App.ViewModels;
using Catogarizer.App.Views;
using Catogarizer.Core.Models;

namespace Catogarizer.App.Services;

public sealed class DialogService : IDialogService
{
    public (string Name, string ExecutablePath, string? Arguments)? ShowAppEdit(AppEditDialogViewModel viewModel)
    {
        var window = new AppEditWindow(viewModel) { Owner = Application.Current.MainWindow };
        var ok = window.ShowDialog();
        return ok == true ? viewModel.Result : null;
    }

    public string? ShowCategoryEdit(CategoryEditDialogViewModel viewModel)
    {
        var window = new CategoryEditWindow(viewModel) { Owner = Application.Current.MainWindow };
        var ok = window.ShowDialog();
        return ok == true ? viewModel.Name.Trim() : null;
    }

    public bool ShowConfirm(string heading, string message, string confirmText = "Delete")
    {
        var window = new ConfirmWindow(heading, message, confirmText) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true;
    }

    public (bool Saved, WindowRect? Placement) ShowPlacement(PlacementDialogViewModel viewModel)
    {
        var window = new PlacementWindow(viewModel) { Owner = Application.Current.MainWindow };
        var ok = window.ShowDialog();
        return (ok == true, viewModel.Result);
    }

    public bool ShowSettings(SettingsViewModel viewModel)
    {
        var window = new SettingsWindow(viewModel) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true;
    }
}
