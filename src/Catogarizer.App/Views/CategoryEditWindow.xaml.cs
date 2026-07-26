using System.Windows;
using System.Windows.Input;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class CategoryEditWindow : Window
{
    private readonly CategoryEditDialogViewModel _viewModel;

    public CategoryEditWindow(CategoryEditDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => NameBox.Focus();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryValidate(out _))
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
