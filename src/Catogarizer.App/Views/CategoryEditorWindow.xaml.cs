using System.Windows;
using System.Windows.Input;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class CategoryEditorWindow : Window
{
    private readonly CategoryEditorViewModel _viewModel;

    public CategoryEditorWindow(CategoryEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Loaded += (_, _) =>
        {
            if (viewModel.SuggestOpenApps && !viewModel.ShowOpenApps)
                viewModel.ToggleOpenAppsCommand.Execute(null);
        };
        Activated += async (_, _) => await viewModel.RefreshOpenAppsAsync();
    }

    /// <summary>The name is saved as you type; Enter just leaves the box.</summary>
    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Keyboard.ClearFocus();
        e.Handled = true;
    }
}
