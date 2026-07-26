using System.Windows;
using Catogarizer.App.ViewModels;
using Catogarizer.App.Views;
using Catogarizer.Core.Models;
using Catogarizer.Core.Services;

namespace Catogarizer.App.Services;

public sealed class DialogService : IDialogService
{
    private readonly IWindowEnumerator _windowEnumerator;
    private readonly IWindowManager _windowManager;

    public DialogService(IWindowEnumerator windowEnumerator, IWindowManager windowManager)
    {
        _windowEnumerator = windowEnumerator;
        _windowManager = windowManager;
    }

    public string? ShowCategoryEdit(Category? existing, IReadOnlyList<Category> allCategories)
    {
        var viewModel = new CategoryEditDialogViewModel(existing, allCategories);
        var window = new CategoryEditWindow(viewModel) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? viewModel.Name.Trim() : null;
    }

    public AppEntry? ShowAppEdit(AppEntry? existing, int order)
    {
        var viewModel = new AppEditDialogViewModel(existing, _windowEnumerator, _windowManager);
        var window = new AppEditWindow(viewModel, existing?.Id, order) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public bool ShowConfirm(string title, string message, string confirmText = "Delete")
    {
        var viewModel = new ConfirmDialogViewModel(title, message, confirmText);
        var window = new ConfirmWindow(viewModel) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true;
    }
}
