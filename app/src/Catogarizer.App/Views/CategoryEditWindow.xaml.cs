using System.Windows;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class CategoryEditWindow : Window
{
    public CategoryEditDialogViewModel ViewModel { get; }

    public CategoryEditWindow(CategoryEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = true;
        Loaded += (_, _) => NameBox.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
