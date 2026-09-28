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
        // Closing with the title-bar X keeps a valid rename, same as Done.
        Closing += (_, _) => { if (!viewModel.IsDeleted) viewModel.CommitName(); };
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

    private void NameBox_LostFocus(object sender, RoutedEventArgs e) => _viewModel.CommitName();

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _viewModel.CommitName();
        Keyboard.ClearFocus();
        e.Handled = true;
    }
}
